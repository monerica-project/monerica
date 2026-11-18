using System.Net;
using System.Text.Json;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    public interface IPriceLookup
    {
        /// <summary>Current USD price of one coin by ticker, or null when unknown.</summary>
        Task<decimal?> UnitUsdAsync(string ticker, CancellationToken ct);
    }

    /// <summary>Spot prices from MoneroMarketCap's public price API (no key needed). Stablecoins are 1.</summary>
    public sealed class PriceLookup : IPriceLookup
    {
        private static readonly HashSet<string> Stable = new (StringComparer.OrdinalIgnoreCase)
        {
            "USDT", "USDC", "DAI", "USD", "BUSD", "TUSD", "FDUSD", "USDE", "PYUSD",
        };

        private readonly HttpClient http;
        private readonly string baseUrl;
        private readonly Dictionary<string, decimal?> cache = new (StringComparer.OrdinalIgnoreCase);

        public PriceLookup(HttpClient http, OrderCheckOptions options)
        {
            this.http = http;
            this.baseUrl = (options.PriceApiBaseUrl ?? string.Empty).TrimEnd('/');
        }

        /// <summary>"USDT (TRC20)", "usdt-trc20", "USDTTRC20" → "USDT"; "btc" → "BTC".</summary>
        public static string CleanTicker(string? raw)
        {
            var letters = new string((raw ?? string.Empty).Trim().ToUpperInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
            foreach (var s in Stable)
            {
                if (letters.StartsWith(s, StringComparison.Ordinal))
                {
                    return s;
                }
            }

            return letters;
        }

        public async Task<decimal?> UnitUsdAsync(string ticker, CancellationToken ct)
        {
            ticker = CleanTicker(ticker);
            if (ticker.Length == 0 || ticker.Length > 10)
            {
                return null;
            }

            if (Stable.Contains(ticker))
            {
                return 1m;
            }

            if (this.cache.TryGetValue(ticker, out var hit))
            {
                return hit;
            }

            decimal? price = null;
            if (this.baseUrl.Length > 0)
            {
                try
                {
                    using var resp = await this.http.GetAsync($"{this.baseUrl}/api/price/{Uri.EscapeDataString(ticker)}", ct);
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                        if (doc.RootElement.TryGetProperty("priceUsd", out var p) && p.TryGetDecimal(out var v) && v > 0)
                        {
                            price = v;
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                }
            }

            this.cache[ticker] = price;
            return price;
        }
    }
}
