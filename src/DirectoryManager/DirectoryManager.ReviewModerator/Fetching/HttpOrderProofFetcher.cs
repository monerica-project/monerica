using System.Net;

namespace DirectoryManager.ReviewModerator.Fetching
{
    /// <summary>
    /// Returns the actual response body (unlike SiteChecker's online-only probe). Keeps two
    /// clients: a clearnet one and a Tor SOCKS5 one (reusing the same proxy the SiteChecker
    /// job relies on), so exchanges that only expose .onion mirrors still resolve.
    /// </summary>
    public sealed class HttpOrderProofFetcher : IOrderProofFetcher, IDisposable
    {
        private const int MaxBodyBytes = 2 * 1024 * 1024; // 2 MB cap; proof pages are tiny
        private readonly HttpClient clearnet;
        private readonly HttpClient? tor;

        public HttpOrderProofFetcher(string userAgent, string torHost, int torPort, bool torAvailable)
        {
            this.clearnet = BuildClient(userAgent, proxy: null);

            if (torAvailable)
            {
                var proxy = new WebProxy($"socks5://{torHost}:{torPort}");
                this.tor = BuildClient(userAgent, proxy);
            }
        }

        public async Task<FetchResult> GetAsync(Uri uri, CancellationToken ct = default)
        {
            var isOnion = uri.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);
            var client = isOnion ? this.tor : this.clearnet;

            if (client is null)
            {
                return new FetchResult { Success = false, Error = "Tor unavailable for .onion host." };
            }

            try
            {
                using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length > MaxBodyBytes)
                {
                    bytes = bytes[..MaxBodyBytes];
                }

                var body = System.Text.Encoding.UTF8.GetString(bytes);

                return new FetchResult
                {
                    Success = resp.IsSuccessStatusCode,
                    StatusCode = (int)resp.StatusCode,
                    Body = body,
                    ContentType = contentType,
                    Error = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}",
                };
            }
            catch (Exception ex)
            {
                return new FetchResult { Success = false, Error = ex.Message };
            }
        }

        public void Dispose()
        {
            this.clearnet.Dispose();
            this.tor?.Dispose();
        }

        private static HttpClient BuildClient(string userAgent, IWebProxy? proxy)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
            };

            if (proxy is not null)
            {
                handler.Proxy = proxy;
                handler.UseProxy = true;
            }

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");
            return client;
        }
    }
}
