using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Helpers
{
    public class StringHelpers
    {
        // =========================
        // Existing helpers (kept)
        // =========================

        public static string UrlKey(string p)
        {
            if (string.IsNullOrWhiteSpace(p))
            {
                return string.Empty;
            }

            // Step 1: Normalize the string to decompose accented characters.
            string normalized = p.Normalize(NormalizationForm.FormD);

            // Step 2: Remove non-ASCII characters (like accents).
            var stringBuilder = new StringBuilder();
            foreach (char c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Step 3: Convert the cleaned string back to normal form.
            string cleaned = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

            // Step 4: Replace special characters with meaningful equivalents.
            cleaned = cleaned.Replace("&", "and");

            // Step 5: Replace non-alphanumeric with a single space.
            var replaceRegex = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s-]+", " ");

            // Step 6: Collapse multiple spaces/dashes into single dash.
            var urlSafe = Regex.Replace(replaceRegex, @"[\s-]+", "-").Trim('-');

            // Step 7: Lowercase.
            return urlSafe.ToLowerInvariant();
        }

        public static string Truncate(string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            {
                return input ?? string.Empty;
            }

            return input.Substring(0, maxLength) + "…";
        }

        public static string TruncateAtWord(string? input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var s = input.Trim();
            if (s.Length <= maxLength)
            {
                return s;
            }

            var cut = s.LastIndexOf(' ', Math.Min(maxLength, s.Length - 1));
            if (cut <= 0)
            {
                cut = maxLength;
            }

            return s[..cut].TrimEnd() + "…";
        }

        public static bool ContainsHyperlink(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var t = text.ToLowerInvariant();

            return t.Contains("http://")
                || t.Contains("https://")
                || t.Contains("www.")
                || t.Contains(".com")
                || t.Contains(".net")
                || t.Contains(".org")
                || t.Contains(".onion");
        }

        // =========================
        // NEW: link / contact helpers
        // =========================

        private static readonly Regex UrlRegex = new (
            @"(?:(?:https?://)|(?:www\.))[\w\-\.]+(?:\.[a-z]{2,})(?:[^\s<>]*)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex EmailRegex = new (
            @"(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Combined “contact field” matcher: finds URLs OR emails in any text.
        private static readonly Regex UrlOrEmailRegex = new (
            @"(?<url>(?:(?:https?://)|(?:www\.))[\w\-\.]+(?:\.[a-z]{2,})(?:[^\s<>]*)?)|(?<email>(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-]))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Encodes text safely for HTML and converts newlines to &lt;br/&gt;.
        /// </summary>
        private static string HtmlEncodeWithLineBreaks(string s)
        {
            // Normalize newlines so we don't double-br.
            return WebUtility.HtmlEncode(s)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br/>");
        }

        private static string NormalizeUrl(string raw)
        {
            var trimmed = raw.Trim();

            // If it’s a www. style URL, assume https
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + trimmed;
            }

            return trimmed;
        }

        private static string TrimTrailingPunctuation(string raw, out string trailing)
        {
            var trimmed = raw.TrimEnd('.', ',', ';', ')', ']', '}', '!', '?', '>', '"', '\'', ':');
            trailing = raw.Substring(trimmed.Length);
            return trimmed;
        }

        private static string BuildExternalLinkHtml(string displayText, string href, string cssClass, bool openInNewTab)
        {
            var safeHref = WebUtility.HtmlEncode(href);
            var safeText = WebUtility.HtmlEncode(displayText);

            var target = openInNewTab ? " target=\"_blank\"" : string.Empty;

            // nofollow/ugc is nice for user-generated content like reviews/contact
            return $"<a class=\"{WebUtility.HtmlEncode(cssClass)}\" href=\"{safeHref}\"{target} rel=\"noopener noreferrer nofollow ugc\">{safeText}</a>";
        }

        private static string ObfuscateToHtmlEntities(string s)
        {
            var sb = new StringBuilder(s.Length * 6);
            foreach (var ch in s)
            {
                sb.Append("&#");
                sb.Append((int)ch);
                sb.Append(';');
            }
            return sb.ToString();
        }

        private static string BuildObfuscatedMailtoHtml(string email, string cssClass)
        {
            // Obfuscate both the visible email and the href email.
            // This stops many dumb scrapers while staying clickable.
            var emailEntities = ObfuscateToHtmlEntities(email);

            // You can also obfuscate "mailto:" itself.
            var mailtoEntities = ObfuscateToHtmlEntities("mailto:");

            return $"<a class=\"{WebUtility.HtmlEncode(cssClass)}\" href=\"{mailtoEntities}{emailEntities}\" rel=\"nofollow noopener noreferrer\">{emailEntities}</a>";
        }

        private static bool IsSingleEmail(string s)
        {
            var t = s.Trim();
            if (t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring("mailto:".Length);
            }

            // Strict-ish full match
            return Regex.IsMatch(t, @"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string ExtractSingleEmail(string s)
        {
            var t = s.Trim();
            if (t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring("mailto:".Length);
            }
            return t.Trim();
        }

        /// <summary>
        /// Review/comment body: linkifies URLs and preserves line breaks.
        /// (Emails are left as plain text here on purpose.)
        /// </summary>
        public static string RenderBodyWithLinksHtml(string? text, string cssClass = "multi-line-text")
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 32);
            int last = 0;

            foreach (Match m in UrlRegex.Matches(text))
            {
                // before match
                if (m.Index > last)
                {
                    sb.Append(HtmlEncodeWithLineBreaks(text.Substring(last, m.Index - last)));
                }

                var raw = m.Value;
                var trimmed = TrimTrailingPunctuation(raw, out var trailing);
                var href = NormalizeUrl(trimmed);

                sb.Append(BuildExternalLinkHtml(trimmed, href, cssClass, openInNewTab: true));

                if (!string.IsNullOrEmpty(trailing))
                {
                    sb.Append(WebUtility.HtmlEncode(trailing));
                }

                last = m.Index + m.Length;
            }

            // remainder
            if (last < text.Length)
            {
                sb.Append(HtmlEncodeWithLineBreaks(text.Substring(last)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Contact field: linkifies URLs (opens in new tab) and converts emails to
        /// an obfuscated mailto link to reduce basic scraping.
        /// Preserves line breaks.
        /// </summary>
        public static string RenderContactFieldHtml(string? text, string cssClass = "multi-line-text")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmedAll = text.Trim();

            // If the entire field is a single email, do the email treatment directly.
            if (IsSingleEmail(trimmedAll))
            {
                var email = ExtractSingleEmail(trimmedAll);
                return BuildObfuscatedMailtoHtml(email, cssClass);
            }

            var sb = new StringBuilder(trimmedAll.Length + 32);
            int last = 0;

            foreach (Match m in UrlOrEmailRegex.Matches(trimmedAll))
            {
                if (m.Index > last)
                {
                    sb.Append(HtmlEncodeWithLineBreaks(trimmedAll.Substring(last, m.Index - last)));
                }

                if (m.Groups["url"].Success)
                {
                    var raw = m.Groups["url"].Value;
                    var trimmed = TrimTrailingPunctuation(raw, out var trailing);
                    var href = NormalizeUrl(trimmed);

                    sb.Append(BuildExternalLinkHtml(trimmed, href, cssClass, openInNewTab: true));

                    if (!string.IsNullOrEmpty(trailing))
                    {
                        sb.Append(WebUtility.HtmlEncode(trailing));
                    }
                }
                else if (m.Groups["email"].Success)
                {
                    var raw = m.Groups["email"].Value;
                    var email = TrimTrailingPunctuation(raw, out var trailing);

                    // If it’s not a plausible single email, just encode it.
                    if (IsSingleEmail(email))
                    {
                        sb.Append(BuildObfuscatedMailtoHtml(ExtractSingleEmail(email), cssClass));
                    }
                    else
                    {
                        sb.Append(WebUtility.HtmlEncode(email));
                    }

                    if (!string.IsNullOrEmpty(trailing))
                    {
                        sb.Append(WebUtility.HtmlEncode(trailing));
                    }
                }

                last = m.Index + m.Length;
            }

            if (last < trimmedAll.Length)
            {
                sb.Append(HtmlEncodeWithLineBreaks(trimmedAll.Substring(last)));
            }

            return sb.ToString();
        }
    }
}
