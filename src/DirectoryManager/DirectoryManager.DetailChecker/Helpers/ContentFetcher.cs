using System.Net;

namespace DirectoryManager.DetailChecker.Helpers
{
    /// <summary>
    /// Fetches a site's readable text for reconfirming details. It pulls the landing page
    /// plus a handful of same-origin pages where country / KYC / contact info usually lives
    /// (about, terms, privacy, faq, contact, …) and returns the combined plain text, capped.
    /// Clearnet goes direct; <c>.onion</c> is routed through the local Tor SOCKS proxy (same
    /// setup the SiteChecker uses). Returns null when the landing page itself can't be
    /// fetched — i.e. the site isn't currently reachable, so we skip it.
    /// </summary>
    public sealed class ContentFetcher : IDisposable
    {
        // Same-origin paths most likely to state jurisdiction / KYC / contact details.
        private static readonly string[] CandidatePaths =
        {
            string.Empty, "/about", "/about-us", "/aboutus", "/terms", "/terms-of-service",
            "/tos", "/privacy", "/privacy-policy", "/faq", "/faqs", "/kyc", "/aml",
            "/legal", "/legal/terms", "/contact", "/contact-us", "/support", "/help",
        };

        private readonly HttpClient clearnet;
        private readonly HttpClient? tor;
        private readonly int maxTotalChars;
        private readonly int maxPagesPerSite;

        public ContentFetcher(string userAgent, string torHost, int torPort, int maxTotalChars = 60_000, int maxPagesPerSite = 6)
        {
            this.maxTotalChars = maxTotalChars;
            this.maxPagesPerSite = maxPagesPerSite;

            this.clearnet = BuildClient(userAgent, proxyUrl: null);

            // Only build the Tor client if the proxy is actually listening; otherwise onion
            // fetches would just hang. The SiteChecker already ensures tor is up in prod.
            this.tor = TorAvailable(torHost, torPort)
                ? BuildClient(userAgent, proxyUrl: $"socks5://{torHost}:{torPort}")
                : null;
        }

        /// <summary>
        /// Returns combined plain text for the site, or null if the landing page is unreachable.
        /// </summary>
        public async Task<string?> FetchSiteTextAsync(string baseUrl, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                return null;
            }

            var isOnion = baseUri.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);
            var client = isOnion ? this.tor : this.clearnet;
            if (client is null)
            {
                return null; // onion but no tor proxy
            }

            var landing = await this.TryFetchTextAsync(client, baseUri, ct);
            if (landing is null)
            {
                return null; // site not reachable → caller skips it
            }

            var combined = new System.Text.StringBuilder();
            combined.Append(landing);
            var pages = 1;

            foreach (var path in CandidatePaths)
            {
                if (path.Length == 0 || pages >= this.maxPagesPerSite || combined.Length >= this.maxTotalChars)
                {
                    continue;
                }

                if (!Uri.TryCreate(baseUri, path, out var pageUri))
                {
                    continue;
                }

                var text = await this.TryFetchTextAsync(client, pageUri, ct);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    combined.Append("\n\n===== ").Append(path).Append(" =====\n").Append(text);
                    pages++;
                }
            }

            var result = combined.ToString();
            return result.Length > this.maxTotalChars ? result[..this.maxTotalChars] : result;
        }

        public void Dispose()
        {
            this.clearnet.Dispose();
            this.tor?.Dispose();
        }

        private static HttpClient BuildClient(string userAgent, string? proxyUrl)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
            };

            if (proxyUrl is not null)
            {
                handler.Proxy = new WebProxy(proxyUrl);
                handler.UseProxy = true;
            }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(25),
                MaxResponseContentBufferSize = 3_000_000, // 3 MB cap per page
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");
            return client;
        }

        private static bool TorAvailable(string host, int port)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var ok = tcp.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2));
                return ok && tcp.Connected;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string?> TryFetchTextAsync(HttpClient client, Uri uri, CancellationToken ct)
        {
            try
            {
                using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }

                var mediaType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    && !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase)
                    && mediaType.Length > 0)
                {
                    return null; // skip binaries / json / etc.
                }

                var html = await resp.Content.ReadAsStringAsync(ct);
                var text = HtmlText.ToPlainText(html);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }
    }
}
