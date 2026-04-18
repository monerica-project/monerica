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

            string normalized = p.Normalize(NormalizationForm.FormD);

            var stringBuilder = new StringBuilder();
            foreach (char c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            string cleaned = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            cleaned = cleaned.Replace("&", "and");

            var replaceRegex = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s-]+", " ");
            var urlSafe = Regex.Replace(replaceRegex, @"[\s-]+", "-").Trim('-');

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
        // Regexes
        // =========================

        private static readonly Regex UrlRegex = new (
            @"(?:(?:https?://)|(?:www\.))[\w\-\.]+(?:\.[a-z]{2,})(?:[^\s<>]*)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex EmailRegex = new (
            @"(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Combined URL or standard email matcher.
        private static readonly Regex UrlOrEmailRegex = new (
            @"(?<url>(?:(?:https?://)|(?:www\.))[\w\-\.]+(?:\.[a-z]{2,})(?:[^\s<>]*)?)|(?<email>(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-]))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Matches obfuscated emails: user [at] domain.com / user(at)domain.com / user AT domain.com
        private static readonly Regex ObfuscatedEmailRegex = new (
            @"(?<![\w.+-])([A-Z0-9._%+\-]+)\s*(?:\[at\]|\(at\)|(?<!\w)at(?!\w))\s*([A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // =========================
        // Private helpers
        // =========================

        /// <summary>
        /// Encodes text safely for HTML and converts newlines to &lt;br/&gt;.
        /// </summary>
        private static string HtmlEncodeWithLineBreaks(string s)
        {
            return WebUtility.HtmlEncode(s)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br/>");
        }

        private static string NormalizeUrl(string raw)
        {
            var trimmed = raw.Trim();

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

        private static string BuildExternalLinkHtml(
            string displayText,
            string href,
            string cssClass,
            bool openInNewTab,
            bool ugcContent = false)
        {
            var safeHref = WebUtility.HtmlEncode(href);
            var safeText = WebUtility.HtmlEncode(displayText);
            var target = openInNewTab ? " target=\"_blank\"" : string.Empty;
            var rel = ugcContent
                ? "noopener noreferrer nofollow ugc"
                : "noopener noreferrer";

            return $"<a class=\"{WebUtility.HtmlEncode(cssClass)}\" href=\"{safeHref}\"{target} rel=\"{rel}\">{safeText}</a>";
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
            var emailEntities = ObfuscateToHtmlEntities(email);
            var mailtoEntities = ObfuscateToHtmlEntities("mailto:");

            return $"<a class=\"{WebUtility.HtmlEncode(cssClass)}\" href=\"{mailtoEntities}{emailEntities}\" rel=\"nofollow noopener noreferrer\">{emailEntities}</a>";
        }

        /// <summary>
        /// Masks the local part of an email address, leaving only the first and last character.
        /// e.g. "rachidbettioui@gmail.com" → "r*************i@gmail.com"
        /// </summary>
        private static string AnonymizeEmail(string email)
        {
            var atIdx = email.IndexOf('@');
            if (atIdx <= 0)
            {
                return email;
            }

            var local = email[..atIdx];
            var domain = email[atIdx..]; // includes '@'

            var maskedLocal = local.Length <= 2
                ? local
                : local[0] + new string('*', local.Length - 2) + local[^1];

            return maskedLocal + domain;
        }

        private static bool IsSingleEmail(string s)
        {
            var t = s.Trim();
            if (t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring("mailto:".Length);
            }

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

        // =========================
        // Public rendering methods
        // =========================

        /// <summary>
        /// Review/comment body: linkifies URLs, anonymizes emails (including [at] variants),
        /// and preserves line breaks.
        /// Pass ugcContent: true for user-generated content to add rel="nofollow ugc" on links.
        /// </summary>
        public static string RenderBodyWithLinksHtml(
            string? text,
            string cssClass = "multi-line-text",
            bool ugcContent = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Pre-process: normalise [at]/(at)/AT obfuscations into anonymized emails
            // so the main loop handles them uniformly.
            text = ObfuscatedEmailRegex.Replace(text, m =>
            {
                var local = m.Groups[1].Value;
                var domain = m.Groups[2].Value;
                return AnonymizeEmail($"{local}@{domain}");
            });

            var sb = new StringBuilder(text.Length + 32);
            int last = 0;

            foreach (Match m in UrlOrEmailRegex.Matches(text))
            {
                if (m.Index > last)
                {
                    sb.Append(HtmlEncodeWithLineBreaks(text.Substring(last, m.Index - last)));
                }

                if (m.Groups["url"].Success)
                {
                    var raw = m.Groups["url"].Value;
                    var trimmed = TrimTrailingPunctuation(raw, out var trailing);
                    var href = NormalizeUrl(trimmed);

                    sb.Append(BuildExternalLinkHtml(trimmed, href, cssClass, openInNewTab: true, ugcContent: ugcContent));

                    if (!string.IsNullOrEmpty(trailing))
                    {
                        sb.Append(WebUtility.HtmlEncode(trailing));
                    }
                }
                else if (m.Groups["email"].Success)
                {
                    var raw = m.Groups["email"].Value;
                    var email = TrimTrailingPunctuation(raw, out var trailing);
                    var masked = AnonymizeEmail(email);

                    sb.Append(WebUtility.HtmlEncode(masked));

                    if (!string.IsNullOrEmpty(trailing))
                    {
                        sb.Append(WebUtility.HtmlEncode(trailing));
                    }
                }

                last = m.Index + m.Length;
            }

            if (last < text.Length)
            {
                sb.Append(HtmlEncodeWithLineBreaks(text.Substring(last)));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convenience overload — defaults ugcContent to false.
        /// </summary>
        public static string RenderBodyWithLinksHtml(string? text, string cssClass = "multi-line-text")
            => RenderBodyWithLinksHtml(text, cssClass, ugcContent: false);

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

        /// <summary>
        /// Anonymizes all emails (standard and [at]/(at)/AT variants) in plain text.
        /// Use before truncating for snippet display.
        /// </summary>
        public static string AnonymizeEmailsInText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Handle [at] / (at) / AT obfuscations first
            text = ObfuscatedEmailRegex.Replace(text, m =>
            {
                var local = m.Groups[1].Value;
                var domain = m.Groups[2].Value;
                return AnonymizeEmail($"{local}@{domain}");
            });

            // Handle standard emails
            text = EmailRegex.Replace(text, m => AnonymizeEmail(m.Value));

            return text;
        }
    }
}