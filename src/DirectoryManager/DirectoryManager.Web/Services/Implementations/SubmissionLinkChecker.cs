using System.Net;
using System.Net.Sockets;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Rejects only clear failures: the domain does not exist, it points to no public address,
    /// or the server refuses the connection. Timeouts, TLS problems, DNS server hiccups and ANY
    /// HTTP status (403 from a bot wall, 500, ...) count as "loads": when in doubt, it is valid.
    /// Connections go through <see cref="PrivateNetworkGuard"/>, so this can't be used to probe
    /// internal addresses.
    /// </summary>
    public sealed class SubmissionLinkChecker : ISubmissionLinkChecker
    {
        public const string NotFoundMessage =
            "This link does not load: the domain \"{0}\" could not be found. Check the address and try again.";

        public const string RefusedMessage =
            "This link does not load: nothing answered at \"{0}\". Check the address and try again.";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

        private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolve;
        private readonly HttpMessageHandler handler;

        public SubmissionLinkChecker()
            : this(
                (host, ct) => Dns.GetHostAddressesAsync(host, ct),
                new SocketsHttpHandler
                {
                    ConnectCallback = PrivateNetworkGuard.SafeConnectAsync,
                    AllowAutoRedirect = false,
                    ConnectTimeout = Timeout,
                })
        {
        }

        public SubmissionLinkChecker(Func<string, CancellationToken, Task<IPAddress[]>> resolve, HttpMessageHandler handler)
        {
            this.resolve = resolve;
            this.handler = handler;
        }

        public async Task<string?> CheckAsync(string url, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null; // format errors are reported by the normal URL validation
            }

            var host = uri.IdnHost;

            // 1) DNS: a name that does not exist is the clearest failure.
            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var literal))
            {
                addresses = new[] { literal };
            }
            else
            {
                try
                {
                    using var dnsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    dnsCts.CancelAfter(Timeout);
                    addresses = await this.resolve(host, dnsCts.Token).ConfigureAwait(false);
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
                {
                    return string.Format(NotFoundMessage, host);
                }
                catch (Exception)
                {
                    return null; // DNS server trouble / timeout: in doubt
                }
            }

            if (addresses.Length == 0)
            {
                return string.Format(NotFoundMessage, host);
            }

            if (addresses.All(PrivateNetworkGuard.IsPrivateOrDisallowedIp))
            {
                return string.Format(NotFoundMessage, host);
            }

            // 2) Does anything answer? Any HTTP response at all means the site exists.
            try
            {
                using var client = new HttpClient(this.handler, disposeHandler: false) { Timeout = Timeout };
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DirectoryLinkCheck/1.0)");
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                return null;
            }
            catch (HttpRequestException ex) when (IsRefused(ex))
            {
                return string.Format(RefusedMessage, host);
            }
            catch (Exception)
            {
                return null; // timeout, TLS, reset, etc.: in doubt
            }
        }

        private static bool IsRefused(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is SocketException se && se.SocketErrorCode is SocketError.ConnectionRefused or SocketError.HostUnreachable or SocketError.NetworkUnreachable)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
