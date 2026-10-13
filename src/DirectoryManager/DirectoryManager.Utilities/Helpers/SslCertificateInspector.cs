using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace DirectoryManager.Utilities.Helpers
{
    /// <summary>
    /// Looks at the TLS certificate a site presents and reports problems a visitor's browser
    /// would warn about: expired, not yet valid, issued for a different domain, or not trusted
    /// (self-signed / unknown issuer). Used by the weekly SiteChecker.
    /// </summary>
    public static class SslCertificateInspector
    {
        /// <summary>
        /// Connects to the URL's host and inspects its certificate.
        /// </summary>
        /// <returns>
        /// Checked = false when the site could not be reached or the handshake failed for a
        /// non-certificate reason (inconclusive: do not count it either way). Otherwise
        /// Issue is null for a healthy certificate, or a short human-readable problem.
        /// </returns>
        public static async Task<(bool Checked, string? Issue)> InspectAsync(string url, TimeSpan timeout, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return (false, null);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            SslPolicyErrors errors = SslPolicyErrors.None;
            X509Certificate2? certificate = null;
            var chainStatuses = new List<X509ChainStatusFlags>();

            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(uri.Host, uri.IsDefaultPort ? 443 : uri.Port, cts.Token).ConfigureAwait(false);

                using var ssl = new SslStream(
                    tcp.GetStream(),
                    false,
                    (_, cert, chain, policyErrors) =>
                    {
                        errors = policyErrors;
                        certificate = cert is null ? null : new X509Certificate2(cert);
                        if (chain is not null)
                        {
                            chainStatuses.AddRange(chain.ChainStatus.Select(s => s.Status));
                        }

                        // Always complete the handshake: we are inspecting, not trusting.
                        return true;
                    });

                await ssl.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = uri.Host,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    },
                    cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException
                                           or System.Security.Authentication.AuthenticationException)
            {
                return (false, null);
            }

            using (certificate)
            {
                if (certificate is null)
                {
                    return (true, "no certificate was presented");
                }

                return (true, Describe(
                    uri.Host,
                    certificate.NotBefore.ToUniversalTime(),
                    certificate.NotAfter.ToUniversalTime(),
                    errors,
                    chainStatuses,
                    certificate.GetNameInfo(X509NameType.DnsName, false),
                    DateTime.UtcNow));
            }
        }

        /// <summary>
        /// Turns raw validation results into one short problem description, or null when the
        /// certificate is fine. Deliberately conservative: an incomplete chain or an unreachable
        /// revocation server is NOT reported, because browsers usually cope with those.
        /// </summary>
        /// <returns>The problem description, or null.</returns>
        public static string? Describe(
            string host,
            DateTime notBeforeUtc,
            DateTime notAfterUtc,
            SslPolicyErrors errors,
            IReadOnlyCollection<X509ChainStatusFlags> chainStatuses,
            string? certificateDnsName,
            DateTime nowUtc)
        {
            if (notAfterUtc < nowUtc)
            {
                return $"expired on {notAfterUtc:yyyy-MM-dd}";
            }

            if (notBeforeUtc > nowUtc)
            {
                return $"not valid until {notBeforeUtc:yyyy-MM-dd}";
            }

            if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                return string.IsNullOrWhiteSpace(certificateDnsName)
                    ? $"issued for a different domain than {host}"
                    : $"issued for a different domain ({certificateDnsName}, not {host})";
            }

            if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors)
                && chainStatuses.Contains(X509ChainStatusFlags.UntrustedRoot))
            {
                return "not trusted (self-signed or unknown issuer)";
            }

            if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            {
                return "no certificate was presented";
            }

            return null;
        }
    }
}
