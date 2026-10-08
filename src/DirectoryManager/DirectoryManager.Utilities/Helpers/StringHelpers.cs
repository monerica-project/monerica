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
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex EmailRegex = new (
            @"(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        // Combined URL or standard email matcher.
        private static readonly Regex UrlOrEmailRegex = new (
            @"(?<url>(?:(?:https?://)|(?:www\.))[\w\-\.]+(?:\.[a-z]{2,})(?:[^\s<>]*)?)|(?<email>(?<![\w.+-])([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-]))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        // Matches obfuscated emails: user [at] domain.com / user(at)domain.com / user AT domain.com
        private static readonly Regex ObfuscatedEmailRegex = new (
            @"(?<![\w.+-])([A-Z0-9._%+\-]+)\s*(?:\[at\]|\(at\)|(?<!\w)at(?!\w))\s*([A-Z0-9.\-]+\.[A-Z]{2,})(?![\w.+-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

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
        /// e.g. "rachidbettioui@gmail.com" → "r*************i@gmail.com".
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

            return Regex.IsMatch(
                t,
                @"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
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
        /// <returns></returns>
        public static string RenderBodyWithLinksHtml(
            string? text,
            string cssClass = "multi-line-text",
            bool ugcContent = false,
            bool maskEmails = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Pre-process: normalise [at]/(at)/AT obfuscations. Public views anonymize them;
            // admin moderation views (maskEmails: false) de-obfuscate to the real address so a
            // moderator can see the contact the author actually supplied.
            text = ObfuscatedEmailRegex.Replace(text, m =>
            {
                var local = m.Groups[1].Value;
                var domain = m.Groups[2].Value;
                var full = $"{local}@{domain}";
                return maskEmails ? AnonymizeEmail(full) : full;
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

                    if (maskEmails)
                    {
                        sb.Append(WebUtility.HtmlEncode(AnonymizeEmail(email)));
                    }
                    else
                    {
                        // Admin moderation view: show the real address (clickable), unmasked.
                        var enc = WebUtility.HtmlEncode(email);
                        sb.Append($"<a href=\"mailto:{enc}\">{enc}</a>");
                    }

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
        /// <returns></returns>
        public static string RenderBodyWithLinksHtml(string? text, string cssClass = "multi-line-text")
            => RenderBodyWithLinksHtml(text, cssClass, ugcContent: false);

        /// <summary>
        /// Contact field: linkifies URLs (opens in new tab) and converts emails to
        /// an obfuscated mailto link to reduce basic scraping.
        /// Preserves line breaks.
        /// </summary>
        /// <returns></returns>
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

        // =========================
        // Contact handle → hyperlink (Social / Messenger)
        // =========================

        // Platform keyword → profile-URL template ({0} = handle without a leading @).
        // Only platforms whose bare handle maps cleanly to a public profile URL.
        private static readonly Dictionary<string, (string Template, string Name)> HandlePlatforms = new (StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = ("https://x.com/{0}", "X"),
            ["twitter"] = ("https://x.com/{0}", "X"),
            ["telegram"] = ("https://t.me/{0}", "Telegram"),
            ["tg"] = ("https://t.me/{0}", "Telegram"),
            ["instagram"] = ("https://instagram.com/{0}", "Instagram"),
            ["ig"] = ("https://instagram.com/{0}", "Instagram"),
            ["github"] = ("https://github.com/{0}", "GitHub"),
            ["youtube"] = ("https://youtube.com/@{0}", "YouTube"),
            ["reddit"] = ("https://www.reddit.com/user/{0}", "Reddit"),
            ["facebook"] = ("https://facebook.com/{0}", "Facebook"),
            ["fb"] = ("https://facebook.com/{0}", "Facebook"),
            ["tiktok"] = ("https://www.tiktok.com/@{0}", "TikTok"),
            ["keybase"] = ("https://keybase.io/{0}", "Keybase"),
        };

        // Platforms we recognise by name but whose bare handle can't be turned into a URL
        // (they need a full invite/link). Their presence blocks the default-platform guess
        // so we never mislink, e.g., "@user on Session" must NOT become an X link.
        private static readonly HashSet<string> NonLinkablePlatforms = new (StringComparer.OrdinalIgnoreCase)
        {
            "signal", "session", "simplex", "matrix", "nostr", "mastodon", "discord",
            "xmpp", "jabber", "threema", "briar", "wire", "wickr", "element",
        };

        // Master matcher for a contact line: scheme URL | email | scheme-less URL | npub |
        // mastodon (@user@instance) | bare @handle. Order matters (mastodon before handle).
        private static readonly Regex ContactTokenRegex = new (
            @"(?<url>(?:https?://|www\.)[^\s<>]+)" +
            @"|(?<email>(?<![\w.+-])[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}(?![\w.+-]))" +
            @"|(?<bareurl>(?<![\w@/])[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)+/[^\s<>]+)" +
            @"|(?<npub>\bnpub1[023456789acdefghjklmnpqrstuvwxyz]{20,}\b)" +
            @"|(?<masto>(?<![\w/@])@[A-Za-z0-9_]{1,64}@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b)" +
            @"|(?<handle>(?<![\w/@])@[A-Za-z0-9_]{2,64}\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(200));

        // An explicitly-named platform: "… on <word>" or a leading "<word>: @handle" /
        // "<word> - @handle". A lone bare word (e.g. "CoinrunnerHQ") is NOT a platform name.
        private static readonly Regex OnPlatformRegex = new (
            @"\bon\s+([A-Za-z][A-Za-z0-9]{1,20})\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex LeadingPlatformRegex = new (
            @"^\s*([A-Za-z][A-Za-z0-9]{1,20})\s*[:\-]\s*@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// Resolves which platform a bare handle in <paramref name="value"/> should link to.
        /// Returns the URL template + display name, or (null, null) when it should stay plain
        /// text (an unknown/non-linkable platform was named, so we must not guess).
        /// </summary>
        private static (string? Template, string? Name) ResolveHandlePlatform(string value, ContactFieldKind kind)
        {
            // A known platform named anywhere in the value wins.
            foreach (Match w in Regex.Matches(value, @"[A-Za-z]+"))
            {
                var word = w.Value;
                if (HandlePlatforms.TryGetValue(word, out var p))
                {
                    return (p.Template, p.Name);
                }

                if (NonLinkablePlatforms.Contains(word))
                {
                    return (null, null); // recognised but not handle-linkable → leave as text
                }
            }

            // A platform named in an explicit "… on <word>" or "<word>: @handle" slot that we
            // don't recognise → don't guess a default (e.g. "@wireprot on Pitch"). Known
            // platforms were already handled by the whole-word scan above.
            foreach (var em in new[] { OnPlatformRegex.Match(value), LeadingPlatformRegex.Match(value) })
            {
                if (em.Success
                    && !HandlePlatforms.ContainsKey(em.Groups[1].Value)
                    && !NonLinkablePlatforms.Contains(em.Groups[1].Value))
                {
                    return (null, null);
                }
            }

            // No platform named → fall back to the field's convention.
            return kind switch
            {
                ContactFieldKind.Social => (HandlePlatforms["x"].Template, HandlePlatforms["x"].Name),
                ContactFieldKind.Messenger => (HandlePlatforms["telegram"].Template, HandlePlatforms["telegram"].Name),
                _ => (null, null),
            };
        }

        /// <summary>
        /// Contact field renderer that turns social/messaging <b>handles</b> into real
        /// hyperlinks. Every resolved contact renders as a single link whose href AND visible
        /// text are the <b>full URL</b> — e.g. a Social "@neir_io" becomes
        /// <c>https://x.com/neir_io</c>, a Messenger "@neir_io telegram" becomes
        /// <c>https://t.me/neir_io</c> — with the original handle/platform words dropped.
        /// A bare handle defaults to X (Social) / Telegram (Messenger). Emails become
        /// obfuscated mailto links (address shown as the text). When nothing can be resolved
        /// to a link (e.g. an unknown platform like "@wireprot on Pitch"), the original text
        /// is preserved so no information is lost.
        /// </summary>
        public static string RenderContactFieldHtml(string? text, ContactFieldKind kind, string cssClass = "multi-line-text")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // Normalise full-width parentheses some users paste around a platform, e.g. （X）.
            var value = text.Replace('（', '(').Replace('）', ')').Trim();

            if (IsSingleEmail(value))
            {
                return BuildObfuscatedMailtoHtml(ExtractSingleEmail(value), cssClass);
            }

            var (template, _) = ResolveHandlePlatform(value, kind);

            var links = new List<string>();
            var unresolvableHandle = false;

            foreach (Match m in ContactTokenRegex.Matches(value))
            {
                if (m.Groups["url"].Success || m.Groups["bareurl"].Success)
                {
                    var trimmed = TrimTrailingPunctuation(m.Value, out _);
                    var href = trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? trimmed
                        : "https://" + (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? trimmed[4..] : trimmed);
                    links.Add(BuildExternalLinkHtml(href, href, cssClass, openInNewTab: true, ugcContent: true));
                }
                else if (m.Groups["email"].Success)
                {
                    var email = TrimTrailingPunctuation(m.Value, out _);
                    if (IsSingleEmail(email))
                    {
                        links.Add(BuildObfuscatedMailtoHtml(ExtractSingleEmail(email), cssClass));
                    }
                    else
                    {
                        unresolvableHandle = true;
                    }
                }
                else if (m.Groups["npub"].Success)
                {
                    var href = "https://njump.me/" + m.Value;
                    links.Add(BuildExternalLinkHtml(href, href, cssClass, openInNewTab: true, ugcContent: true));
                }
                else if (m.Groups["masto"].Success)
                {
                    // @user@instance.tld → https://instance.tld/@user
                    var parts = m.Value.TrimStart('@').Split('@');
                    var href = $"https://{parts[1]}/@{parts[0]}";
                    links.Add(BuildExternalLinkHtml(href, href, cssClass, openInNewTab: true, ugcContent: true));
                }
                else if (m.Groups["handle"].Success)
                {
                    if (template != null)
                    {
                        var href = string.Format(CultureInfo.InvariantCulture, template, m.Value.TrimStart('@'));
                        links.Add(BuildExternalLinkHtml(href, href, cssClass, openInNewTab: true, ugcContent: true));
                    }
                    else
                    {
                        unresolvableHandle = true; // named an unknown/non-linkable platform
                    }
                }
            }

            // Whole field is a single bare word (no @, no URL) → handle for the default platform.
            if (links.Count == 0 && !unresolvableHandle && template != null
                && Regex.IsMatch(value, @"^[A-Za-z0-9_.]{2,64}$"))
            {
                var href = string.Format(CultureInfo.InvariantCulture, template, value.TrimStart('@'));
                links.Add(BuildExternalLinkHtml(href, href, cssClass, openInNewTab: true, ugcContent: true));
            }

            // Everything resolved → show ONLY the full-URL link(s); drop platform words/labels.
            if (links.Count > 0 && !unresolvableHandle)
            {
                return string.Join("<br/>", links);
            }

            // Couldn't fully resolve → keep the original text (still linkifying any plain
            // URLs/emails inline) so nothing is lost and nothing is mislinked.
            return RenderContactFieldHtml(value, cssClass);
        }

        /// <summary>
        /// Anonymizes all emails (standard and [at]/(at)/AT variants) in plain text.
        /// Use before truncating for snippet display.
        /// </summary>
        /// <returns></returns>
        public static string AnonymizeEmailsInText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

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