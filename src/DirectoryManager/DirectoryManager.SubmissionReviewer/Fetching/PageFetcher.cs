using System.Net;
using System.Net.Sockets;
using System.Text;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;

namespace DirectoryManager.SubmissionReviewer.Fetching
{
    /// <summary>Result of fetching one page.</summary>
    public sealed record FetchResult(
        string Url,
        bool Reachable,
        int? StatusCode,
        string? Html,
        string? Error,
        Uri? FinalUri)
    {
        /// <summary>The site answered but refuses bots (Cloudflare, rate limits): reachable, just unreadable.</summary>
        public bool Blocked => this.StatusCode is 401 or 403 or 429 or 503;
    }

    public interface IPageFetcher
    {
        Task<FetchResult> FetchAsync(string url, CancellationToken ct);
    }

    /// <summary>
    /// Plain GET (no JavaScript), 10 s timeout, 2 MB cap, honest User-Agent, one retry.
    /// .onion addresses go through the local Tor SOCKS proxy.
    /// </summary>
    public sealed class PageFetcher : IPageFetcher, IDisposable
    {
        private const int MaxBytes = 2 * 1024 * 1024;
        private readonly HttpClient clearnet;
        private readonly HttpClient? tor;

        public PageFetcher(ReviewerOptions options)
        {
            this.clearnet = Create(options.UserAgent, null);
            if (TorAvailable(options.TorHost, options.TorPort))
            {
                this.tor = Create(options.UserAgent, new WebProxy($"socks5://{options.TorHost}:{options.TorPort}"));
            }
        }

        public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
        {
            if (!Uri.TryCreate(NormalizeScheme(url), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new FetchResult(url, false, null, null, "Invalid URL", null);
            }

            var isOnion = UrlNormalizer.IsOnion(url);
            var client = isOnion ? this.tor : this.clearnet;
            if (client == null)
            {
                // Can't check an onion without Tor: treat it as reachable so it isn't auto-rejected.
                return new FetchResult(url, true, null, null, "Not checked (Tor unavailable)", uri);
            }

            FetchResult? last = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                last = await this.TryOnceAsync(client, uri, isOnion ? 40 : 10, ct);
                if (last.Reachable && last.StatusCode is >= 200 and < 300)
                {
                    return last;
                }

                if (last.Blocked)
                {
                    return new FetchResult(last.Url, true, last.StatusCode, last.Html, last.Error, last.FinalUri);
                }

                if (attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
            }

            return last!;
        }

        public void Dispose()
        {
            this.clearnet.Dispose();
            this.tor?.Dispose();
        }

        private static string NormalizeScheme(string url)
        {
            var u = (url ?? string.Empty).Trim();
            return u.Contains("://", StringComparison.Ordinal) ? u : "https://" + u;
        }

        private static HttpClient Create(string userAgent, IWebProxy? proxy)
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                Proxy = proxy,
                UseProxy = proxy != null,
            };

            // Clearnet only (Tor refuses internal addresses itself): submitted links, redirects and
            // links found on pages must never reach loopback / LAN / link-local / metadata
            // addresses on this server. Checked on every connect, so redirects are covered and the
            // validated IP is the one used (no DNS-rebinding window). (Audit 2026-11-11.)
            if (proxy == null)
            {
                handler.ConnectCallback = SafeConnectAsync;
            }
            var http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.5");
            return http;
        }

        /// <summary>Connects only when every resolved address is public (see NetworkAddressHelper).</summary>
        internal static async ValueTask<Stream> SafeConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal)
                ? new[] { literal }
                : await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);

            if (addresses.Length == 0)
            {
                throw new HttpRequestException($"Could not resolve host '{host}'.");
            }

            if (addresses.Any(DirectoryManager.Utilities.Helpers.NetworkAddressHelper.IsPrivateOrDisallowed))
            {
                throw new HttpRequestException($"Blocked: '{host}' resolves to a private or internal address.");
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static bool TorAvailable(string host, int port)
        {
            try
            {
                using var c = new TcpClient();
                return c.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2)) && c.Connected;
            }
            catch
            {
                return false;
            }
        }

        private async Task<FetchResult> TryOnceAsync(HttpClient client, Uri uri, int timeoutSeconds, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var status = (int)resp.StatusCode;
                var finalUri = resp.RequestMessage?.RequestUri ?? uri;
                string? html = null;
                var type = resp.Content.Headers.ContentType?.MediaType ?? "text/html";
                if (type.Contains("html", StringComparison.OrdinalIgnoreCase) || type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                    type.Contains("javascript", StringComparison.OrdinalIgnoreCase))
                {
                    html = await ReadCappedAsync(resp.Content, cts.Token);
                }

                return new FetchResult(uri.ToString(), status is >= 200 and < 400, status, html, status is >= 200 and < 300 ? null : $"HTTP {status}", finalUri);
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException se)
            {
                return new FetchResult(uri.ToString(), false, null, null, $"Connection failed ({se.SocketErrorCode})", null);
            }
            catch (HttpRequestException ex)
            {
                return new FetchResult(uri.ToString(), false, null, null, "Connection failed: " + ex.Message, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new FetchResult(uri.ToString(), false, null, null, $"Timed out after {timeoutSeconds}s", null);
            }
        }

        private static async Task<string> ReadCappedAsync(HttpContent content, CancellationToken ct)
        {
            await using var stream = await content.ReadAsStreamAsync(ct);
            var buffer = new byte[81920];
            using var ms = new MemoryStream();
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, Math.Min(read, MaxBytes - (int)ms.Length));
                if (ms.Length >= MaxBytes)
                {
                    break;
                }
            }

            var charset = content.Headers.ContentType?.CharSet;
            Encoding enc;
            try
            {
                enc = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                enc = Encoding.UTF8;
            }

            return enc.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }
    }
}
