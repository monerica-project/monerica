using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using DirectoryManager.ReviewModerator.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.ReviewModerator.Pricing
{
    /// <summary>
    /// USD price source backed by the MoneroMarketCap spot-price API
    /// (<c>GET {baseUrl}/api/price/{symbol}</c>, added in MoneroMarketCap commit "api for price").
    /// The endpoint serves the latest price straight from MMC's Coins table (kept fresh by MMC's
    /// own worker), so it needs no API key and carries no upstream rate-limit risk.
    ///
    /// Caveats handled here:
    ///  - The API is spot-only (no historical/as-of lookup), so <c>asOfUtc</c> is recorded in the
    ///    note but the latest price is used. That is acceptable: the money-band tag is best-effort
    ///    and completion — not valuation — is the hard approval gate.
    ///  - Unknown ticker (404), unreachable, bad response, or unconfigured base URL all resolve to
    ///    <see cref="PriceLookupResult.Unavailable"/>, so the moderator still approves a completed
    ///    order and simply skips the money band rather than failing.
    ///
    /// Config key: <c>MoneroMarketCap:PriceApiBaseUrl</c> (e.g. "https://moneromarketcap.com"; the
    /// "/api/price/{symbol}" path is appended here). Leave empty to keep the previous "unavailable"
    /// behaviour.
    /// </summary>
    public sealed class MoneroMarketCapPriceLookupService : IPriceLookupService, IDisposable
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly string? baseUrl;
        private readonly HttpClient http;

        // Per-run memo of unit USD price by ticker, so a batch of reviews that all sent the same
        // asset (XMR/BTC/USDT) does not re-hit the endpoint once per review.
        private readonly ConcurrentDictionary<string, (decimal UnitUsd, DateTime FetchedUtc)> cache =
            new (StringComparer.OrdinalIgnoreCase);

        public MoneroMarketCapPriceLookupService(IConfiguration config)
        {
            this.baseUrl = config["MoneroMarketCap:PriceApiBaseUrl"]?.Trim().TrimEnd('/');

            var userAgent = config["UserAgent:Header"]
                ?? "Mozilla/5.0 (compatible; MonericaReviewModerator/1.0; +https://monerica.com)";

            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
            this.http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            this.http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public async Task<PriceLookupResult> GetUsdValueAsync(
            string assetSymbol,
            decimal amount,
            DateTime? asOfUtc,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(this.baseUrl))
            {
                return PriceLookupResult.Unavailable(
                    "MoneroMarketCap price API not configured (MoneroMarketCap:PriceApiBaseUrl).");
            }

            if (string.IsNullOrWhiteSpace(assetSymbol) || amount <= 0)
            {
                return PriceLookupResult.Unavailable("Missing asset symbol or non-positive amount.");
            }

            var ticker = assetSymbol.Trim().ToUpperInvariant();

            var unit = await this.GetUnitUsdAsync(ticker, ct);
            if (!unit.Available || unit.UsdValue is not { } unitUsd)
            {
                return unit;
            }

            var total = amount * unitUsd;
            var spotNote = asOfUtc.HasValue ? "; spot price (API has no historical lookup)" : string.Empty;

            return new PriceLookupResult
            {
                Available = true,
                UsdValue = total,
                Note = $"{ticker}@{unitUsd:0.########} USD{spotNote}",
            };
        }

        public void Dispose() => this.http.Dispose();

        private async Task<PriceLookupResult> GetUnitUsdAsync(string ticker, CancellationToken ct)
        {
            if (this.cache.TryGetValue(ticker, out var hit) &&
                DateTime.UtcNow - hit.FetchedUtc < CacheTtl)
            {
                return PriceLookupResult.Found(hit.UnitUsd);
            }

            Uri uri;
            try
            {
                uri = new Uri($"{this.baseUrl}/api/price/{Uri.EscapeDataString(ticker)}");
            }
            catch (UriFormatException ex)
            {
                return PriceLookupResult.Unavailable($"Bad price API base URL: {ex.Message}");
            }

            try
            {
                using var resp = await this.http.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);

                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    return PriceLookupResult.Unavailable($"MoneroMarketCap has no price for ticker '{ticker}'.");
                }

                if (!resp.IsSuccessStatusCode)
                {
                    return PriceLookupResult.Unavailable($"Price API returned HTTP {(int)resp.StatusCode}.");
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("priceUsd", out var priceEl) ||
                    priceEl.ValueKind != JsonValueKind.Number ||
                    !priceEl.TryGetDecimal(out var unitUsd) ||
                    unitUsd <= 0)
                {
                    return PriceLookupResult.Unavailable(
                        $"Price API response missing a usable priceUsd for '{ticker}'.");
                }

                this.cache[ticker] = (unitUsd, DateTime.UtcNow);
                return PriceLookupResult.Found(unitUsd);
            }
            catch (Exception ex)
            {
                return PriceLookupResult.Unavailable($"Price API unreachable: {ex.Message}");
            }
        }
    }
}