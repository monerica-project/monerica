using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Display + summary formatting for deposit guarantees (amount + currency). A single amount
    /// renders like "30K USD" / "1 BTC"; a summary totals PER CURRENCY, e.g. 10K USD + 5K USD + 1 BTC
    /// becomes "15K USD, 1 BTC".
    /// </summary>
    public static class DepositGuaranteeHelper
    {
        /// <summary>Currencies offered for a deposit guarantee (and the display/summary order).</summary>
        public static readonly IReadOnlyList<Currency> Currencies = new[] { Currency.USD, Currency.BTC, Currency.XMR };

        /// <summary>Formats one amount, e.g. (30000, USD) -> "30K USD"; (0.5m, BTC) -> "0.5 BTC".</summary>
        public static string FormatAmount(decimal amount, Currency currency)
        {
            return Abbreviate(amount) + " " + currency.ToString();
        }

        /// <summary>
        /// Link text for a guarantee's proof URL: the bare hostname (no scheme, no "www.", no path),
        /// e.g. "https://orangefren.com/x" -> "orangefren.com", "bitcointalk.org/index.php?t=1" ->
        /// "bitcointalk.org". Falls back to "view proof" when the URL can't be parsed.
        /// </summary>
        public static string ProofLinkLabel(string? url)
        {
            const string fallback = "view proof";
            if (string.IsNullOrWhiteSpace(url))
            {
                return fallback;
            }

            var s = url.Trim();
            if (!s.Contains("://", StringComparison.Ordinal))
            {
                s = "http://" + s;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                return fallback;
            }

            var host = uri.Host;
            if (string.IsNullOrEmpty(host))
            {
                return fallback;
            }

            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring(4);
            }

            return string.IsNullOrEmpty(host) ? fallback : host;
        }

        /// <summary>Totals the guarantees per currency and renders them, e.g. "15K USD, 1 BTC".</summary>
        public static string SummarizeByCurrency(IEnumerable<GuaranteeItem>? guarantees)
        {
            var totals = new Dictionary<Currency, decimal>();
            foreach (var g in guarantees ?? Enumerable.Empty<GuaranteeItem>())
            {
                if (g == null)
                {
                    continue;
                }

                var amount = g.Amount.GetValueOrDefault();
                if (amount <= 0)
                {
                    continue;
                }

                totals[g.Currency] = totals.TryGetValue(g.Currency, out var running) ? running + amount : amount;
            }

            // Fixed order first, then any stray currency not in the list (safety).
            var parts = Currencies
                .Where(totals.ContainsKey)
                .Select(c => FormatAmount(totals[c], c))
                .ToList();

            parts.AddRange(totals
                .Where(kv => !Currencies.Contains(kv.Key))
                .Select(kv => FormatAmount(kv.Value, kv.Key)));

            return string.Join(", ", parts);
        }

        private static string Abbreviate(decimal amount)
        {
            if (amount < 0)
            {
                amount = 0;
            }

            if (amount >= 1_000_000m)
            {
                return Trim(amount / 1_000_000m) + "M";
            }

            if (amount >= 1_000m)
            {
                return Trim(amount / 1_000m) + "K";
            }

            return Trim(amount);
        }

        // Up to 12 decimals (full Monero precision), trailing zeros dropped (30 -> "30", 0.125 -> "0.125").
        private static string Trim(decimal value) =>
            value.ToString("0.############", CultureInfo.InvariantCulture);
    }
}
