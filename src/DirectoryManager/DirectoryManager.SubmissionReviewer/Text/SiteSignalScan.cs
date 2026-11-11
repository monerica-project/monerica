using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Small evidence scans the page text alone can miss: links to API / developer docs, the
    /// site's legal pages (terms, FAQ, AML/KYC policy), and the sentences on those pages that say
    /// anything about KYC / ID checks. Pure functions; the reviewer does the fetching.
    /// </summary>
    public static class SiteSignalScan
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Path segments such as /api, /api-docs, /apidocs, /developers, /docs/api, /swagger, /openapi.json
        private static readonly Regex ApiPathRx = new Regex(
            @"(?:^|/)(?:api|api-?docs?|api-?reference|developers?|dev-?docs|swagger(?:-ui)?|openapi|redoc)(?:$|[/.\-?#])",
            Opts, Timeout);

        private static readonly Regex ApiTextRx = new Regex(
            @"\b(?:API|APIs|REST API|API docs?|API documentation|API reference|developers?|developer docs|for developers|swagger|openapi)\b",
            Opts, Timeout);

        private static readonly Regex LegalRx = new Regex(
            @"\b(?:terms|tos|terms-of-(?:service|use)|legal|faq|faqs|aml|kyc|aml-kyc|kyc-aml|policy|policies|rules)\b",
            Opts, Timeout);

        private static readonly Regex KycSentenceRx = new Regex(
            @"\b(?:KYC|AML|know your customer|identity verification|verify (?:your )?identity|identification|ID (?:check|verification|documents?)|passport|selfie|source of funds|proof of (?:identity|address)|freez(?:e|es|ing)|frozen|withh(?:o|e)ld|hold (?:or refuse|funds|transactions)|refuse transactions|compliance|sanction(?:s|ed)?)\b",
            Opts, Timeout);

        private static readonly Regex SentenceSplitRx = new Regex(@"(?<=[.!?])\s+", Opts, Timeout);

        /// <summary>Links on the page to API / developer documentation ("API docs (https://x.com/api-docs/)").</summary>
        public static IReadOnlyList<string> ApiLinks(string? html, Uri pageUri) =>
            HtmlText.Links(html, pageUri)
                .Where(l => (Uri.TryCreate(l.Url, UriKind.Absolute, out var u) && ApiPathRx.IsMatch(u.AbsolutePath))
                            || ApiTextRx.IsMatch(l.Text ?? string.Empty))
                .Select(l => string.IsNullOrWhiteSpace(l.Text) ? l.Url : $"{l.Text} ({l.Url})")
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();

        /// <summary>
        /// Same-site legal pages worth reading for the KYC policy: the submitter's extra links first
        /// (Link 2/3, often the terms), then terms / FAQ / AML-KYC links found on the page.
        /// </summary>
        public static IReadOnlyList<string> LegalPages(string? html, Uri pageUri, IEnumerable<string?> extraLinks, int max = 2)
        {
            var list = new List<string>();
            void Add(string url)
            {
                var key = UrlNormalizer.Normalize(url);
                if (key != UrlNormalizer.Normalize(pageUri.ToString()) && !list.Any(x => UrlNormalizer.Normalize(x) == key))
                {
                    list.Add(url);
                }
            }

            foreach (var link in extraLinks)
            {
                if (!string.IsNullOrWhiteSpace(link) && Uri.TryCreate(link.Trim(), UriKind.Absolute, out var u) &&
                    string.Equals(UrlNormalizer.Host(u.ToString()), UrlNormalizer.Host(pageUri.ToString()), StringComparison.OrdinalIgnoreCase))
                {
                    Add(u.ToString());
                }
            }

            foreach (var l in HtmlText.Links(html, pageUri))
            {
                if (list.Count >= max)
                {
                    break;
                }

                if (Uri.TryCreate(l.Url, UriKind.Absolute, out var u) &&
                    string.Equals(UrlNormalizer.Host(u.ToString()), UrlNormalizer.Host(pageUri.ToString()), StringComparison.OrdinalIgnoreCase) &&
                    (LegalRx.IsMatch(u.AbsolutePath.Replace('_', '-')) || LegalRx.IsMatch(l.Text ?? string.Empty)))
                {
                    Add(u.GetLeftPart(UriPartial.Path));
                }
            }

            return list.Take(max).ToList();
        }

        /// <summary>Sentences in a page's text that mention KYC / ID checks / holding funds (distinct, trimmed).</summary>
        public static IReadOnlyList<string> KycSentences(string? html, int max = 6)
        {
            var body = HtmlText.Extract(html, maxBody: 40000).Body ?? string.Empty;
            return SentenceSplitRx.Split(body)
                .Select(s => s.Trim())
                .Where(s => s.Length is >= 8 and <= 400 && KycSentenceRx.IsMatch(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }
    }
}
