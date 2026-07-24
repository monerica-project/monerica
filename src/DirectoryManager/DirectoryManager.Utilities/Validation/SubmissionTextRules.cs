using System.Text;
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Content rules applied to public submissions: the blocked-term blacklist and the
    /// "don't start the description with the listing name" rule.
    /// </summary>
    public static class SubmissionTextRules
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Lower-cases, converts separators people use interchangeably (/, -, _, +, &amp;,
        /// commas, pipes) into spaces, drops any other punctuation, and collapses runs of
        /// whitespace. This is what makes "No KYC/AML", "no kyc - aml" and "NO KYC AML"
        /// all normalize to the same thing, so one blocked term catches every spelling.
        /// </summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);

            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch) || ch == '/' || ch == '\\' || ch == '-'
                         || ch == '_' || ch == '+' || ch == '&' || ch == ',' || ch == '|'
                         || ch == '.' || ch == ':' || ch == ';')
                {
                    sb.Append(' ');
                }

                // Any other punctuation is dropped entirely (e.g. "n.o-a*m*l" quirks).
            }

            return CollapseSpaces(sb.ToString());
        }

        /// <summary>
        /// True when <paramref name="text"/> contains <paramref name="term"/>. Both sides
        /// are normalized first; <c>*</c> in the term means "anything in between", so
        /// "no*aml" matches "no AML" and "no KYC/AML" alike. Matching is on whole words so
        /// a term like "aml" does not fire inside "amly" or "camlink".
        /// </summary>
        public static bool ContainsTerm(string? text, string? term)
        {
            var haystack = Normalize(text);
            var needle = Normalize(term);

            if (haystack.Length == 0 || needle.Length == 0)
            {
                return false;
            }

            // Normalize() strips '*', so detect the wildcard on the raw term.
            var hasWildcard = (term ?? string.Empty).Contains('*', StringComparison.Ordinal);

            if (!hasWildcard)
            {
                return Regex.IsMatch(
                    haystack,
                    $@"(?<![a-z0-9]){Regex.Escape(needle)}(?![a-z0-9])",
                    RegexOptions.None,
                    RegexTimeout);
            }

            // Rebuild the pattern from the raw term so wildcard positions are preserved:
            // each '*'-separated chunk is normalized and escaped, joined by "anything".
            var chunks = (term ?? string.Empty)
                .Split('*', StringSplitOptions.None)
                .Select(Normalize)
                .Where(c => c.Length > 0)
                .Select(Regex.Escape)
                .ToList();

            if (chunks.Count == 0)
            {
                return false;
            }

            var pattern = $@"(?<![a-z0-9]){string.Join(@"[a-z0-9 ]{0,40}?", chunks)}(?![a-z0-9])";

            try
            {
                return Regex.IsMatch(haystack, pattern, RegexOptions.None, RegexTimeout);
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological term must never take the submit page down; treat as no match.
                return false;
            }
        }

        /// <summary>
        /// True when <paramref name="description"/> opens by repeating the listing
        /// <paramref name="name"/> — the "Acme Wallet — Acme Wallet is a wallet that..."
        /// pattern. Leading punctuation and quotes are ignored, and a trailing domain
        /// suffix on the name ("Acme.com") is tolerated so "Acme is..." still trips it.
        /// </summary>
        public static bool DescriptionStartsWithName(string? description, string? name)
        {
            var desc = Normalize(description);
            var listing = Normalize(name);

            if (desc.Length == 0 || listing.Length == 0)
            {
                return false;
            }

            if (StartsWithWord(desc, listing))
            {
                return true;
            }

            // "Acme.com" normalizes to "acme com"; also try the leading part before the
            // domain suffix so a description opening with just "Acme" is still caught.
            var parts = listing.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 1 && IsDomainSuffix(parts[^1]))
            {
                var withoutSuffix = string.Join(' ', parts[..^1]);
                return withoutSuffix.Length > 0 && StartsWithWord(desc, withoutSuffix);
            }

            return false;
        }

        private static bool StartsWithWord(string haystack, string needle)
        {
            if (!haystack.StartsWith(needle, StringComparison.Ordinal))
            {
                return false;
            }

            // Must end on a word boundary: "acme" should not match "acmecorp does...".
            return haystack.Length == needle.Length || haystack[needle.Length] == ' ';
        }

        private static bool IsDomainSuffix(string token) => token switch
        {
            "com" or "net" or "org" or "io" or "co" or "cash" or "app" or "xyz"
                or "info" or "biz" or "me" or "to" or "is" or "onion" or "i2p" => true,
            _ => false,
        };

        private static string CollapseSpaces(string value)
        {
            var sb = new StringBuilder(value.Length);
            var lastWasSpace = false;

            foreach (var ch in value)
            {
                if (ch == ' ')
                {
                    if (!lastWasSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(ch);
                    lastWasSpace = false;
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
