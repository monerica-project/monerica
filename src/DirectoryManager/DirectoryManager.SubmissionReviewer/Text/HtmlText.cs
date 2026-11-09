using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>What the dossier needs from one fetched page.</summary>
    public sealed record PageText(string Title, string MetaDescription, string Body, IReadOnlyList<string> PaymentLines);

    /// <summary>A link on a page: absolute URL and its visible text.</summary>
    public sealed record PageLink(string Url, string Text);

    /// <summary>Plain-text extraction from HTML without running scripts (regex based, no dependencies).</summary>
    public static class HtmlText
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private static readonly Regex DropBlocks = new (@"<(script|style|noscript|svg|template|iframe|head)\b[^>]*>.*?</\1\s*>", Opts, Timeout);
        private static readonly Regex Comments = new (@"<!--.*?-->", Opts, Timeout);
        private static readonly Regex BlockTags = new (@"<\s*/?\s*(p|div|li|ul|ol|br|h[1-6]|tr|td|th|table|section|article|header|footer|nav|main|aside|form|label|button|option|dd|dt|blockquote)\b[^>]*>", Opts, Timeout);
        private static readonly Regex AnyTag = new (@"<[^>]+>", Opts, Timeout);
        private static readonly Regex Spaces = new (@"[ \t\f\v\u00a0]+", Opts, Timeout);
        private static readonly Regex TitleRx = new (@"<title\b[^>]*>(.*?)</title>", Opts, Timeout);
        private static readonly Regex MetaRx = new (@"<meta\b[^>]*>", Opts, Timeout);
        private static readonly Regex AttrRx = new ("(?<name>[a-zA-Z:-]+)\\s*=\\s*(?:\"(?<v>[^\"]*)\"|'(?<v>[^']*)')", Opts, Timeout);
        private static readonly Regex AnchorRx = new (@"<a\b(?<attrs>[^>]*)>(?<text>.*?)</a\s*>", Opts, Timeout);
        private static readonly Regex PaymentRx = new (@"monero|\bxmr\b|cryptocurrenc|accepted payments?|payment methods?|we accept|pay with", Opts, Timeout);
        private static readonly Regex PayLinkRx = new (@"monero|\bxmr\b|\bpay(ment)?s?\b|checkout|pricing|\bplans?\b|\border\b|\bbuy\b|\bdonate\b|faq", Opts, Timeout);

        public static PageText Extract(string? html, int maxBody = 3000)
        {
            html ??= string.Empty;
            var title = Clean(TitleRx.Match(html).Groups[1].Value);
            var meta = MetaDescription(html);

            var text = Comments.Replace(html, " ");
            text = DropBlocks.Replace(text, " ");
            text = BlockTags.Replace(text, "\n");
            text = AnyTag.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);

            var lines = text.Split('\n')
                .Select(l => Spaces.Replace(l, " ").Trim())
                .Where(l => l.Length > 1)
                .ToList();

            var payment = lines
                .Where(l => PaymentRx.IsMatch(l))
                .Select(l => l.Length > 240 ? l[..240] + "…" : l)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(15)
                .ToList();

            var body = string.Join(" ", lines);
            if (body.Length > maxBody)
            {
                body = body[..maxBody] + "…";
            }

            return new PageText(title, meta, body, payment);
        }

        /// <summary>
        /// The best page to fetch second: a same-site link whose text or URL mentions Monero/XMR first,
        /// then payment/checkout/pricing-type links. Null when there is none.
        /// </summary>
        public static string? FindPaymentLink(string? html, Uri pageUri)
        {
            var links = Links(html, pageUri)
                .Where(l => SameSite(l.Url, pageUri))
                .Where(l => !string.Equals(UrlNormalizer.Normalize(l.Url), UrlNormalizer.Normalize(pageUri.ToString()), StringComparison.Ordinal))
                .ToList();

            var monero = links.FirstOrDefault(l => Regex.IsMatch(l.Text + " " + l.Url, @"monero|\bxmr\b", Opts, Timeout));
            if (monero != null)
            {
                return monero.Url;
            }

            return links.FirstOrDefault(l => PayLinkRx.IsMatch(l.Text + " " + l.Url))?.Url;
        }

        public static IEnumerable<PageLink> Links(string? html, Uri baseUri)
        {
            foreach (Match m in AnchorRx.Matches(html ?? string.Empty))
            {
                var href = Attr(m.Groups["attrs"].Value, "href");
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') ||
                    href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Uri.TryCreate(baseUri, WebUtility.HtmlDecode(href.Trim()), out var abs) ||
                    (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps))
                {
                    continue;
                }

                yield return new PageLink(abs.GetLeftPart(UriPartial.Query), Clean(AnyTag.Replace(m.Groups["text"].Value, " ")));
            }
        }

        public static int CountUrls(string? text) =>
            Regex.Matches(text ?? string.Empty, @"https?://|www\.|\b[a-z0-9-]+\.(com|net|org|io|xyz|ru|top|shop)\b", Opts, Timeout).Count;

        private static string MetaDescription(string html)
        {
            foreach (Match m in MetaRx.Matches(html))
            {
                var name = Attr(m.Value, "name") ?? Attr(m.Value, "property");
                if (name != null && (name.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                                     name.Equals("og:description", StringComparison.OrdinalIgnoreCase)))
                {
                    return Clean(Attr(m.Value, "content"));
                }
            }

            return string.Empty;
        }

        private static string? Attr(string tag, string name)
        {
            foreach (Match a in AttrRx.Matches(tag))
            {
                if (a.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return a.Groups["v"].Value;
                }
            }

            return null;
        }

        private static bool SameSite(string url, Uri page)
        {
            var h = UrlNormalizer.Host(url);
            var p = UrlNormalizer.Host(page.ToString());
            return h == p || h.EndsWith("." + p, StringComparison.Ordinal) || p.EndsWith("." + h, StringComparison.Ordinal);
        }

        private static string Clean(string? s) =>
            Spaces.Replace(WebUtility.HtmlDecode(s ?? string.Empty).Replace('\n', ' ').Replace('\r', ' '), " ").Trim();
    }
}
