using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds fact-based meta descriptions of at most <see cref="MaxLength"/> characters.
    /// A listing's description leads with what the directory knows for sure (subcategory,
    /// status, KYC policy, liquidity, rating, Tor/I2P), then fills the rest with the
    /// listing's own description, cut at a word boundary.
    /// </summary>
    public static class SeoDescriptionBuilder
    {
        /// <summary>Search engines show about 155-160 characters.</summary>
        public const int MaxLength = 160;

        // Below this many characters left, the free-text description is dropped entirely
        // rather than shown as a stub.
        private const int MinDescriptionBudget = 25;

        /// <summary>
        /// e.g. "Trocador (Aggregators, Monero): Guaranteed No KYC. Rated 4.7/5 from 31 reviews.
        /// Tor site. A privacy-focused exchange aggregator with minimal JavaScript."
        /// </summary>
        public static string ForListing(ListingDescriptionFacts facts)
        {
            ArgumentNullException.ThrowIfNull(facts);

            var name = Clean(facts.Name);
            var sub = Clean(facts.SubcategoryName);
            var currency = SeoTitleBuilder.ShortCurrency(facts.CurrencyName);

            var lead = name;
            if (sub.Length > 0)
            {
                lead += currency.Length > 0 && !sub.Contains(currency, StringComparison.OrdinalIgnoreCase)
                    ? $" ({currency} {sub})"
                    : $" ({sub})";
            }

            var parts = new List<string>();

            switch (facts.Status)
            {
                case DirectoryStatus.Scam:
                    parts.Add("Flagged as a scam");
                    break;
                case DirectoryStatus.Questionable:
                    parts.Add("Marked questionable");
                    break;
                case DirectoryStatus.Verified:
                    parts.Add("Verified");
                    break;
            }

            var kyc = KycFact(facts.KycPolicy);
            if (kyc.Length > 0)
            {
                parts.Add(kyc);
            }

            if (facts.Liquidity is Liquidity.Own or Liquidity.Mixed or Liquidity.ThirdParty
                or Liquidity.OwnAndThirdParty or Liquidity.VariesByProvider)
            {
                parts.Add($"{EnumHelper.GetDescription(facts.Liquidity)} liquidity");
            }

            if (facts.ReviewCount > 0 && facts.AverageRating.HasValue)
            {
                var avg = Math.Round(facts.AverageRating.Value, 1).ToString("0.#", CultureInfo.InvariantCulture);
                parts.Add($"Rated {avg}/5 from {facts.ReviewCount} review{(facts.ReviewCount == 1 ? string.Empty : "s")}");
            }

            var networks = new List<string>();
            if (facts.HasLink2 && !string.IsNullOrWhiteSpace(facts.Link2Name))
            {
                networks.Add(facts.Link2Name.Trim());
            }

            if (facts.HasLink3 && !string.IsNullOrWhiteSpace(facts.Link3Name))
            {
                networks.Add(facts.Link3Name.Trim());
            }

            if (networks.Count > 0)
            {
                parts.Add($"{string.Join(" and ", networks)} site{(networks.Count > 1 ? "s" : string.Empty)} available");
            }

            var factText = parts.Count == 0 ? lead + "." : $"{lead}: {string.Join(". ", parts)}.";
            if (factText.Length > MaxLength)
            {
                return SeoTitleBuilder.Truncate(factText, MaxLength - 1).TrimEnd('.') + ".";
            }

            var description = Clean(facts.Description);
            var budget = MaxLength - factText.Length - 1;
            if (description.Length == 0 || budget < MinDescriptionBudget)
            {
                return factText;
            }

            return $"{factText} {Shorten(description, budget)}";
        }

        /// <summary>
        /// Fallback for a category/subcategory with no saved meta description, e.g.
        /// "Browse 42 Instant Swaps (Exchanges) that accept or relate to Monero (XMR) on
        /// Monerica. Compare KYC policies, ratings and reviews."
        /// </summary>
        public static string ForListPage(string pageName, int listingCount, string? currencyName, string? siteName)
        {
            var name = Clean(pageName);
            var currency = Clean(currencyName);
            var site = Clean(siteName);

            var count = listingCount > 0 ? $"{listingCount} " : string.Empty;
            var relate = currency.Length > 0 ? $" that accept or relate to {currency}" : string.Empty;
            var on = site.Length > 0 ? $" on {site}" : string.Empty;

            var text = $"Browse {count}{name} listings{relate}{on}. Compare KYC policies, user ratings, reviews and Tor/I2P availability.";
            return Shorten(text, MaxLength);
        }

        /// <summary>Cleans whitespace and cuts to <paramref name="max"/> characters at a word boundary.</summary>
        public static string Shorten(string? text, int max = MaxLength)
        {
            var clean = Clean(text);
            if (clean.Length <= max)
            {
                return clean;
            }

            // Leave room for the ellipsis.
            var cut = SeoTitleBuilder.Truncate(clean, max - 1).TrimEnd('.', ',', ';', ':', ' ');
            return cut + "…";
        }

        private static string KycFact(KycPolicy? policy) => policy switch
        {
            KycPolicy.GuaranteedNoKyc => "Guaranteed no KYC",
            KycPolicy.RareKyc => "Rare KYC",
            KycPolicy.ShotgunKyc => "Shotgun KYC",
            KycPolicy.MandatoryKyc => "Mandatory KYC",
            KycPolicy.VariesByProvider => "KYC varies by provider",
            _ => string.Empty,
        };

        private static string Clean(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : Regex.Replace(text.Trim(), "\\s+", " ");
    }
}
