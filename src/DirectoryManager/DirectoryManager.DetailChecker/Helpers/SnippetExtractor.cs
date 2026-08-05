using System.Text.RegularExpressions;

namespace DirectoryManager.DetailChecker.Helpers
{
    /// <summary>
    /// Deterministic (no-AI) extraction. Emails and contact handles are precise with regex,
    /// so we never bother a model with them. For the semantic fields (country / KYC) we can't
    /// classify with regex, but we CAN cheaply locate the relevant paragraph and hand only
    /// that snippet to the small local model — which keeps it fast and cheap on CPU.
    /// </summary>
    public static class SnippetExtractor
    {
        private static readonly Regex EmailRx = new(
            @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
            RegexOptions.Compiled);

        // Substrings that mark an address as NOT the listing's real contact: placeholders, asset
        // filenames, third-party boilerplate (GitHub/Sentry/CDNs), and auto-generated no-reply
        // senders. For a crypto directory the service's contact is essentially never @github.com,
        // so those boilerplate copyright@/noreply@ addresses are dropped.
        private static readonly string[] EmailNoise =
        {
            "example.com", "example.org", "example.net", "email.com", "domain.com", "yourdomain",
            "yoursite", "test@", "user@", "name@",
            "@2x", ".png", ".jpg", ".jpeg", ".webp", ".svg", ".gif", ".ico", ".css", ".js",
            "@github.com", "githubusercontent", "@sentry", "sentry.io", "sentry-next",
            "wixpress.com", "cloudflare", "@w3.org", "schema.org", "googleapis", "gstatic",
            "jsdelivr", "unpkg", "polyfill", "core-js", "cdn.",
            "noreply", "no-reply", "donotreply", "do-not-reply", "copyright@", "mailer-daemon",
        };

        private static readonly Regex[] ContactRx =
        {
            new(@"https?://t\.me/[A-Za-z0-9_/+]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new(@"https?://signal\.me/[#A-Za-z0-9_/+\-=]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new(@"https?://simplex\.chat/[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new(@"https?://(?:www\.)?(?:twitter|x)\.com/[A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new(@"https?://[A-Za-z0-9.\-]+/@[A-Za-z0-9_]+", RegexOptions.IgnoreCase | RegexOptions.Compiled), // mastodon
            new(@"@[A-Za-z0-9_]+:[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled),                     // matrix id
            new(@"session:?\s*[0-9a-f]{66}", RegexOptions.IgnoreCase | RegexOptions.Compiled),               // session id
        };

        // Pages, in priority order, whose text most often lets a reader (or a model) INFER the
        // KYC stance and jurisdiction — even when the words "KYC" or "registered in" never appear.
        // We feed the model the actual prose from these, not keyword windows, because sites almost
        // never state these things verbatim and describe them inconsistently.
        private static readonly string[] RelevantSectionOrder =
        {
            "/terms", "/terms-of-service", "/tos", "/privacy", "/privacy-policy", "/kyc", "/aml",
            "/legal", "/legal/terms", "/faq", "/faqs", "/about", "/about-us", "/aboutus",
            "/support", "/help", "/contact", "/contact-us",
        };

        public static List<string> ExtractEmails(string text)
        {
            return EmailRx.Matches(text)
                .Select(m => m.Value.Trim().ToLowerInvariant())
                .Where(e => !EmailNoise.Any(n => e.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .Take(10)
                .ToList();
        }

        public static List<string> ExtractContacts(string text)
        {
            var found = new List<string>();
            foreach (var rx in ContactRx)
            {
                found.AddRange(rx.Matches(text).Select(m => m.Value.Trim()));
            }

            return found
                .Where(c => c.Length is > 4 and < 300)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// Picks the text most useful for INFERRING country + KYC and returns it, capped so a
        /// small CPU model stays fast. The <see cref="ContentFetcher"/> output is the landing
        /// page followed by "===== /path =====" sections; we take the terms/privacy/faq/about
        /// sections first (that's where the real policy lives), then fall back to the landing
        /// page. The model reads this prose and decides — we do NOT pre-judge with keywords,
        /// because sites express these things in wildly different words (or imply them).
        /// </summary>
        public static string RelevantTextForModel(string fullText, int cap = 7000)
        {
            if (string.IsNullOrWhiteSpace(fullText))
            {
                return string.Empty;
            }

            // Split into (label, body). Everything before the first marker is the landing page.
            var parts = fullText.Split("\n\n===== ", StringSplitOptions.None);
            var landing = parts[0];
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < parts.Length; i++)
            {
                var marker = parts[i].IndexOf(" =====\n", StringComparison.Ordinal);
                if (marker <= 0)
                {
                    continue;
                }

                var label = parts[i][..marker].Trim();
                var body = parts[i][(marker + " =====\n".Length)..].Trim();
                if (!sections.ContainsKey(label) && body.Length > 0)
                {
                    sections[label] = body;
                }
            }

            var sb = new System.Text.StringBuilder();

            void Add(string title, string body)
            {
                if (sb.Length >= cap || string.IsNullOrWhiteSpace(body))
                {
                    return;
                }

                var remaining = cap - sb.Length;
                var slice = body.Length > remaining ? body[..remaining] : body;
                sb.Append('[').Append(title).Append("]\n").Append(slice).Append("\n\n");
            }

            foreach (var path in RelevantSectionOrder)
            {
                if (sections.TryGetValue(path, out var body))
                {
                    Add(path, body);
                }
            }

            // Always include some of the landing page — for many small sites it's the only text,
            // and it often carries the "no account needed / registered in X" signal.
            Add("home", landing);

            var result = sb.ToString().Trim();
            return result.Length > cap ? result[..cap] : result;
        }
    }
}
