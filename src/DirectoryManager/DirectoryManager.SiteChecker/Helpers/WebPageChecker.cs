using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class WebPageChecker
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        // Per-address connect ceiling. A black-holed address (e.g. broken IPv6
        // egress) is bounded by this, but it never delays the verdict because a
        // healthy address in the other family wins the race first.
        private static readonly TimeSpan PerAddressConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient http;
        private readonly DiagnosticLogger log;
        private readonly HashSet<string> skipHosts;

        public WebPageChecker(
            string userAgent,
            TimeSpan? timeout = null,
            DiagnosticLogger? logger = null,
            IEnumerable<string>? skipHosts = null)
        {
            this.log = logger ?? new DiagnosticLogger(null);

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,

                // This is a REACHABILITY probe, not a security-sensitive client.
                // A self-signed / expired / incomplete-chain cert must not read
                // as "offline".
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (m, c, ch, e) => true
                },

                // Happy Eyeballs (RFC 8305), the curl behavior. Without this,
                // SocketsHttpHandler connecting by hostname can stall on a dead
                // AAAA address until timeout — the exact cause of the IPv6
                // black-hole false positives. ConnectTimeout is intentionally
                // not set: it is ignored when ConnectCallback is supplied, and
                // PerAddressConnectTimeout governs instead.
                ConnectCallback = async (context, ct) =>
                {
                    var socket = await HappyEyeballs
                        .ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, PerAddressConnectTimeout, ct)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };

            this.http = new HttpClient(handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };

            // A complete browser fingerprint. Requests missing Accept /
            // Accept-Language are a primary bot signal, and some origins (ATS,
            // mod_security) tarpit the response when they see a crawler UA. The
            // UA value itself comes from config — make sure it is a real browser
            // string, not an identifier like "MonericaSiteChecker/1.0".
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            this.http.DefaultRequestHeaders.Accept.ParseAdd(
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            this.http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            this.skipHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (skipHosts != null)
            {
                foreach (var h in skipHosts)
                {
                    var norm = NormalizeHost(h);
                    if (!string.IsNullOrWhiteSpace(norm))
                    {
                        this.skipHosts.Add(norm);
                    }
                }
            }
        }

        public async Task<bool> IsOnlineAsync(Uri uri)
        {
            if (this.IsSkipped(uri))
            {
                this.Verdict(uri, true, "skip-list (manually verified)");
                return true;
            }

            var attemptSummaries = new List<string>();

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                var (result, summary) = await this.TryOnceAsync(uri, attempt);
                attemptSummaries.Add(summary);

                if (result.HasValue)
                {
                    this.Verdict(uri, result.Value, summary);
                    if (!result.Value)
                    {
                        this.log.LogOfflineFailure(uri.ToString(), "clearnet", attemptSummaries);
                    }

                    return result.Value;
                }

                if (attempt < MaxRetries)
                {
                    this.log.Log($"[clearnet] Retrying {uri} in {RetryDelay.TotalSeconds}s (attempt {attempt}/{MaxRetries})...");
                    await Task.Delay(RetryDelay);
                }
            }

            // All HTTP attempts inconclusive — probe reachability directly.
            bool reachable = await this.ProbeReachableAsync(uri);
            if (reachable)
            {
                this.Verdict(uri, true, "HTTP inconclusive; reachability probe succeeded");
                return true;
            }

            this.Verdict(uri, false, "HTTP inconclusive; reachability probe failed");
            this.log.LogOfflineFailure(uri.ToString(), "clearnet", attemptSummaries);
            return false;
        }

        private static string NormalizeHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var v = value.Trim();
            if (Uri.TryCreate(v, UriKind.Absolute, out var parsed))
            {
                v = parsed.Host;
            }

            v = v.TrimEnd('/').ToLowerInvariant();
            if (v.StartsWith("www.", StringComparison.Ordinal))
            {
                v = v.Substring(4);
            }

            return v;
        }

        // Writes a one-line verdict (online AND offline) to the diagnostic FILE,
        // so the run-over-run audit trail shows everything — not just offline
        // verdicts, which is why false negatives were previously invisible.
        private void Verdict(Uri uri, bool online, string reason)
        {
            this.log.LogImportant($"VERDICT {(online ? "ONLINE " : "OFFLINE")} clearnet {uri} — {reason}");
        }

        private bool IsSkipped(Uri uri)
        {
            if (this.skipHosts.Count == 0)
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host.Substring(4);
            }

            return this.skipHosts.Contains(host);
        }

        private async Task<bool> ProbeReachableAsync(Uri uri)
        {
            var isHttps = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            var port = uri.IsDefaultPort
                ? (isHttps ? 443 : 80)
                : uri.Port;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                // Same Happy Eyeballs path as the HTTP client, so the probe is
                // not itself defeated by a dead IPv6 address.
                using var socket = await HappyEyeballs
                    .ConnectAsync(uri.Host, port, PerAddressConnectTimeout, cts.Token)
                    .ConfigureAwait(false);

                if (isHttps)
                {
                    try
                    {
                        using var ns = new NetworkStream(socket, ownsSocket: false);
                        using var ssl = new SslStream(ns, false, (s, c, ch, e) => true);
                        await ssl
                            .AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = uri.Host }, cts.Token)
                            .ConfigureAwait(false);

                        this.log.Log($"[clearnet probe] {uri.Host}:{port} → TLS handshake OK (server up; HTTP layer blocked by WAF/bot rules)");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        this.log.Log($"[clearnet probe] {uri.Host}:{port} → TCP open, TLS handshake failed ({DiagnosticLogger.SummarizeException(ex)}); treating as UP");
                        return true;
                    }
                }

                this.log.Log($"[clearnet probe] {uri.Host}:{port} → TCP open; treating as UP");
                return true;
            }
            catch (Exception ex)
            {
                this.log.Log($"[clearnet probe] {uri.Host} → probe failed: {DiagnosticLogger.SummarizeException(ex)}");
                return false;
            }
        }

        private async Task<(bool? result, string summary)> TryOnceAsync(Uri uri, int attempt)
        {
            // 1) HEAD
            var headSw = Stopwatch.StartNew();
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, uri);
                using var headResp = await this.http
                    .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                headSw.Stop();
                var statusCode = (int)headResp.StatusCode;
                var server = headResp.Headers.TryGetValues("Server", out var sv) ? string.Join(",", sv) : string.Empty;
                var cfRay = headResp.Headers.TryGetValues("CF-Ray", out var cr) ? string.Join(",", cr) : string.Empty;

                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} → {statusCode} ({headSw.ElapsedMilliseconds}ms) Server={server} CF-Ray={cfRay}");

                if (statusCode is >= 200 and < 400)
                {
                    return (true, $"attempt {attempt}: HEAD {statusCode} in {headSw.ElapsedMilliseconds}ms");
                }

                // Everything else (403/405/429/503, Cloudflare 52x, etc.) is NOT
                // a verdict — fall through to GET, then the reachability probe.
            }
            catch (TaskCanceledException)
            {
                headSw.Stop();
                var summary = $"attempt {attempt}: HEAD timeout after {headSw.ElapsedMilliseconds}ms";
                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                headSw.Stop();
                var headFailSummary = $"HEAD failed ({headSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} — {headFailSummary}");

                // fall through to GET
            }

            // 2) GET
            var getSw = Stopwatch.StartNew();
            try
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResp = await this.http
                    .SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                getSw.Stop();
                var statusCode = (int)getResp.StatusCode;
                var server = getResp.Headers.TryGetValues("Server", out var sv) ? string.Join(",", sv) : string.Empty;
                var cfRay = getResp.Headers.TryGetValues("CF-Ray", out var cr) ? string.Join(",", cr) : string.Empty;

                this.log.Log($"[clearnet GET attempt {attempt}] {uri} → {statusCode} ({getSw.ElapsedMilliseconds}ms) Server={server} CF-Ray={cfRay}");

                // Only a genuine "resource is gone" counts as offline. A 403/429/
                // 503/52x means the server is up but guarding itself.
                if (statusCode == 404 || statusCode == 410)
                {
                    return (false, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms Server={server}");
                }

                return (true, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms");
            }
            catch (TaskCanceledException)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET timeout after {getSw.ElapsedMilliseconds}ms";
                this.log.Log($"[clearnet GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET failed ({getSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[clearnet GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
        }
    }
}