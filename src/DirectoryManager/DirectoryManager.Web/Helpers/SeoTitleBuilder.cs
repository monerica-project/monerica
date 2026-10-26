namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds short, keyword-first page &lt;title&gt;s that stay within the length search engines
    /// show (about 60 characters). Each builder tries the most descriptive form first and drops
    /// parts in a fixed order until one fits: the " | SiteName" brand goes first, then filler
    /// words, and only as a last resort is the title cut at a word boundary.
    /// Nothing here is hardcoded to one brand or currency: the site name and the currency name
    /// ("Monero (XMR)") come from config, because the codebase is forked.
    /// </summary>
    public static class SeoTitleBuilder
    {
        /// <summary>The longest title we emit. Titles longer than this get cut off in results.</summary>
        public const int MaxLength = 60;

        private const string BrandSeparator = " | ";

        // Words in a category name that say nothing about the subject.
        private static readonly HashSet<string> CategoryStopWords = new (StringComparer.OrdinalIgnoreCase)
        {
            "and", "&", "the", "for", "other", "official", "non",
        };

        /// <summary>
        /// "Monero (XMR)" -> "Monero": the part before " (". Blank in, blank out.
        /// </summary>
        public static string ShortCurrency(string? currencyName)
        {
            if (string.IsNullOrWhiteSpace(currencyName))
            {
                return string.Empty;
            }

            var name = currencyName.Trim();
            var paren = name.IndexOf(" (", StringComparison.Ordinal);
            return paren > 0 ? name.Substring(0, paren).Trim() : name;
        }

        /// <summary>
        /// Returns the first candidate that fits <see cref="MaxLength"/>. If none fits, the last
        /// (shortest) candidate is cut at a word boundary. Blank candidates are skipped.
        /// </summary>
        public static string FirstThatFits(params string?[] candidates)
        {
            string? last = null;
            foreach (var raw in candidates)
            {
                var candidate = Clean(raw);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (candidate.Length <= MaxLength)
                {
                    return candidate;
                }

                last = candidate;
            }

            return last == null ? string.Empty : Truncate(last, MaxLength);
        }

        /// <summary>
        /// Adds " | SiteName" to a title when the result still fits and the title does not
        /// already name the site. Otherwise returns the title as is (cut to fit if needed).
        /// </summary>
        public static string WithBrand(string? title, string? siteName)
        {
            var clean = Clean(title);
            var brand = Clean(siteName);

            if (clean.Length == 0)
            {
                return brand;
            }

            if (brand.Length == 0 || clean.Contains(brand, StringComparison.OrdinalIgnoreCase))
            {
                return FirstThatFits(clean);
            }

            return FirstThatFits(clean + BrandSeparator + brand, clean);
        }

        /// <summary>
        /// Listing profile title, e.g. "Trocador: Monero Aggregator Reviews | Monerica".
        /// Drops the brand first, then "Reviews", then the currency, then the subcategory.
        /// </summary>
        public static string ForListing(
            string listingName,
            string? subcategoryName,
            string? currencyName,
            string? siteName,
            int page = 1)
        {
            var name = Clean(listingName);
            var sub = Singularize(Clean(subcategoryName));
            var currency = ShortCurrency(currencyName);
            var pageSuffix = PageSuffix(page);

            // Avoid "Monero Monero Hardware" when the subcategory already names the currency.
            var currencyPrefix = currency.Length == 0 || sub.Contains(currency, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : currency + " ";

            var withCurrency = sub.Length == 0 ? currency : currencyPrefix + sub;
            var subject = withCurrency.Length == 0 ? name : $"{name}: {withCurrency}";
            var subjectNoCurrency = sub.Length == 0 ? name : $"{name}: {sub}";

            return FirstThatFits(
                Brand($"{subject} Reviews{pageSuffix}", siteName),
                $"{subject} Reviews{pageSuffix}",
                $"{subject}{pageSuffix}",
                $"{subjectNoCurrency}{pageSuffix}",
                Brand($"{name} Reviews{pageSuffix}", siteName),
                $"{name} Reviews{pageSuffix}",
                $"{name}{pageSuffix}");
        }

        /// <summary>
        /// Subcategory title, keyword first. When the subcategory name already carries the
        /// category's subject ("Mobile Wallets" in Wallets) it reads
        /// "Mobile Wallets for Monero (XMR) | Monerica"; otherwise the category is added as
        /// context: "Instant Swaps: Monero (XMR) Exchanges | Monerica".
        /// </summary>
        public static string ForSubcategory(
            string subcategoryName,
            string categoryName,
            string? currencyName,
            string? siteName,
            int page = 1)
        {
            var sub = Clean(subcategoryName);
            var cat = Clean(categoryName);
            var currency = Clean(currencyName);
            var shortCurrency = ShortCurrency(currencyName);
            var pageSuffix = PageSuffix(page);

            string Build(string cur)
            {
                if (cur.Length == 0)
                {
                    return SharesSubject(sub, cat) || cat.Length == 0 ? sub : $"{sub}: {cat}";
                }

                return SharesSubject(sub, cat) || cat.Length == 0
                    ? $"{sub} for {cur}"
                    : $"{sub}: {cur} {cat}";
            }

            var full = Build(currency);
            var shorter = Build(shortCurrency);
            var bare = Build(string.Empty);

            return FirstThatFits(
                Brand(full + pageSuffix, siteName),
                full + pageSuffix,
                Brand(shorter + pageSuffix, siteName),
                shorter + pageSuffix,
                bare + pageSuffix,
                sub + pageSuffix);
        }

        /// <summary>
        /// Category title, keyword first: "Exchanges for Monero (XMR) | Monerica".
        /// </summary>
        public static string ForCategory(
            string categoryName,
            string? currencyName,
            string? siteName,
            int page = 1)
        {
            var cat = Clean(categoryName);
            var currency = Clean(currencyName);
            var shortCurrency = ShortCurrency(currencyName);
            var pageSuffix = PageSuffix(page);

            var full = currency.Length == 0 ? cat : $"{cat} for {currency}";
            var shorter = shortCurrency.Length == 0 ? cat : $"{cat} for {shortCurrency}";

            return FirstThatFits(
                Brand(full + pageSuffix, siteName),
                full + pageSuffix,
                Brand(shorter + pageSuffix, siteName),
                shorter + pageSuffix,
                cat + pageSuffix);
        }

        /// <summary>
        /// A title built from a subject plus a short descriptor, e.g. ("No KYC", "Monero (XMR)
        /// Listings") -> "No KYC: Monero (XMR) Listings | Monerica", dropping the brand, then the
        /// descriptor, to fit.
        /// </summary>
        public static string ForSubject(string subject, string? descriptor, string? siteName, int page = 1)
        {
            var s = Clean(subject);
            var d = Clean(descriptor);
            var pageSuffix = PageSuffix(page);
            var full = d.Length == 0 ? s : $"{s}: {d}";

            return FirstThatFits(
                Brand(full + pageSuffix, siteName),
                full + pageSuffix,
                Brand(s + pageSuffix, siteName),
                s + pageSuffix);
        }

        /// <summary>
        /// Very small English singularizer for the LAST word of a subcategory name
        /// ("Instant Swaps" -> "Instant Swap", "Communities" -> "Community", "VPNs" -> "VPN").
        /// Names ending in a parenthetical, and words like "News" or "Statistics", are left alone.
        /// </summary>
        public static string Singularize(string? phrase)
        {
            var text = Clean(phrase);
            if (text.Length == 0 || text.EndsWith(')'))
            {
                return text;
            }

            var lastSpace = text.LastIndexOfAny(new[] { ' ', '/', '-' });
            var head = lastSpace >= 0 ? text.Substring(0, lastSpace + 1) : string.Empty;
            var word = lastSpace >= 0 ? text.Substring(lastSpace + 1) : text;

            return head + SingularizeWord(word);
        }

        internal static string SingularizeWord(string word)
        {
            if (word.Length < 3)
            {
                return word;
            }

            var lower = word.ToLowerInvariant();

            // Words that end in "s" but are not plurals (or whose singular reads wrong).
            if (lower is "news" or "series" or "species" or "gas" or "bus" or "plus" or "status"
                || lower.EndsWith("ss", StringComparison.Ordinal)
                || lower.EndsWith("us", StringComparison.Ordinal)
                || lower.EndsWith("is", StringComparison.Ordinal)
                || lower.EndsWith("ics", StringComparison.Ordinal))
            {
                return word;
            }

            if (lower.EndsWith("ies", StringComparison.Ordinal) && word.Length > 4)
            {
                return word.Substring(0, word.Length - 3) + (char.IsUpper(word[^1]) ? "Y" : "y");
            }

            if (lower.EndsWith("sses", StringComparison.Ordinal)
                || lower.EndsWith("shes", StringComparison.Ordinal)
                || lower.EndsWith("ches", StringComparison.Ordinal)
                || lower.EndsWith("xes", StringComparison.Ordinal))
            {
                return word.Substring(0, word.Length - 2);
            }

            if (lower.EndsWith('s'))
            {
                return word.Substring(0, word.Length - 1);
            }

            return word;
        }

        /// <summary>
        /// True when the subcategory name already contains one of the category's subject words
        /// ("Mobile Wallets" / "Wallets", "Tor &amp; Onion Hosting" / "Web Hosting").
        /// </summary>
        internal static bool SharesSubject(string subcategoryName, string categoryName)
        {
            if (string.IsNullOrWhiteSpace(subcategoryName) || string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            var sub = subcategoryName.ToLowerInvariant();
            var words = categoryName
                .Split(new[] { ' ', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3 && !CategoryStopWords.Contains(w));

            foreach (var w in words)
            {
                var stem = Stem(w);
                if (stem.Length >= 3 && sub.Contains(stem, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Cuts text to at most <paramref name="max"/> characters at a word boundary.</summary>
        internal static string Truncate(string text, int max)
        {
            if (text.Length <= max)
            {
                return text;
            }

            var cut = text.Substring(0, max);
            var space = cut.LastIndexOf(' ');
            if (space > max / 2)
            {
                cut = cut.Substring(0, space);
            }

            return cut.TrimEnd(' ', ':', '|', '-', ',', '&');
        }

        private static string Stem(string word)
        {
            // Acronyms ("SMS", "VPN") are compared as is.
            if (word.Length <= 4 && word.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                return word.ToLowerInvariant();
            }

            word = word.ToLowerInvariant();
            if (word.EndsWith("ies", StringComparison.Ordinal))
            {
                return word.Substring(0, word.Length - 3);
            }

            if (word.EndsWith("es", StringComparison.Ordinal) && word.Length > 4)
            {
                return word.Substring(0, word.Length - 2);
            }

            if (word.EndsWith('s'))
            {
                return word.Substring(0, word.Length - 1);
            }

            return word;
        }

        private static string Brand(string title, string? siteName)
        {
            var brand = Clean(siteName);
            if (brand.Length == 0 || title.Contains(brand, StringComparison.OrdinalIgnoreCase))
            {
                return title;
            }

            return title + BrandSeparator + brand;
        }

        private static string PageSuffix(int page) => page > 1 ? $" (Page {page})" : string.Empty;

        private static string Clean(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : System.Text.RegularExpressions.Regex.Replace(text.Trim(), "\\s+", " ");
    }
}
