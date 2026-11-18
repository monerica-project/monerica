using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Picks out of a page's text only the sentences that say something about the facts a listing
    /// carries (identity checks, countries served, VPN/Tor, fees and limits), and fingerprints them.
    /// Comparing fingerprints month to month is free; the AI is only asked when the wording changed.
    /// </summary>
    public static partial class TermsSignalText
    {
        public const int MaxSentences = 80;

        /// <summary>Relevant sentences in page text: distinct, trimmed, 20–400 characters.</summary>
        public static List<string> RelevantSentences(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var found = new List<string>();
            foreach (var raw in SplitRegex().Split(text))
            {
                var s = SpaceRegex().Replace(raw, " ").Trim();
                if (s.Length is < 20 or > 400 || !SignalRegex().IsMatch(s))
                {
                    continue;
                }

                if (seen.Add(Normalize(s)))
                {
                    found.Add(s);
                }
            }

            return found;
        }

        /// <summary>
        /// Form used for comparing: lower case, punctuation and spacing collapsed, and things that
        /// change without the policy changing (years, dates, long numbers such as counters) removed.
        /// </summary>
        public static string Normalize(string sentence)
        {
            var s = sentence.ToLowerInvariant();
            s = VolatileRegex().Replace(s, " ");
            s = NonWordRegex().Replace(s, " ");
            return SpaceRegex().Replace(s, " ").Trim();
        }

        /// <summary>The comparison set for a listing: normalized, sorted, capped.</summary>
        public static List<string> ComparisonSet(IEnumerable<string> sentences) =>
            sentences.Select(Normalize).Where(s => s.Length >= 12).Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).Take(MaxSentences).ToList();

        public static string Fingerprint(IEnumerable<string> comparisonSet) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", comparisonSet)))).ToLowerInvariant();

        /// <summary>Sentences in the new text that were not there before, and old ones that are gone.</summary>
        public static (List<string> Added, List<string> Removed) Diff(IEnumerable<string> oldSentences, IEnumerable<string> newSentences)
        {
            var oldList = oldSentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var newList = newSentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var oldKeys = oldList.Select(Normalize).ToHashSet(StringComparer.Ordinal);
            var newKeys = newList.Select(Normalize).ToHashSet(StringComparer.Ordinal);
            return (
                newList.Where(s => !oldKeys.Contains(Normalize(s))).ToList(),
                oldList.Where(s => !newKeys.Contains(Normalize(s))).ToList());
        }

        [GeneratedRegex(@"(?<=[.!?])\s+|[\r\n]+|\s{3,}", RegexOptions.None, 2000)]
        private static partial Regex SplitRegex();

        [GeneratedRegex(@"\s+", RegexOptions.None, 2000)]
        private static partial Regex SpaceRegex();

        [GeneratedRegex(@"[^a-z0-9%$]+", RegexOptions.None, 2000)]
        private static partial Regex NonWordRegex();

        // Years, dates, times and long digit runs (order numbers, counters, block heights, prices of coins).
        [GeneratedRegex(@"\b(?:19|20)\d{2}\b|\b\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4}\b|\b\d{1,2}:\d{2}(?::\d{2})?\b|\b\d[\d,.]{4,}\b|\b(?:january|february|march|april|may|june|july|august|september|october|november|december)\b", RegexOptions.IgnoreCase, 2000)]
        private static partial Regex VolatileRegex();

        [GeneratedRegex(
            @"\b(?:KYC|AML|know your customer|identity|identification|verif(?:y|ied|ication)|passport|selfie|source of funds|proof of (?:identity|address)|freez\w*|frozen|withh[oe]ld\w*|sanction\w*|restricted|prohibited|not (?:available|permitted|allowed|offered|supported)|residents?|citizens?|jurisdictions?|countr(?:y|ies)|territor(?:y|ies)|geo-?block\w*|VPN|proxy|proxies|Tor\b|onion|anonymi[sz]\w*|fees?|commission|surcharge|minimum|maximum|limits?|refund\w*|custod\w*|no logs?|logs? (?:are|is) (?:kept|stored|deleted))\b",
            RegexOptions.IgnoreCase, 2000)]
        private static partial Regex SignalRegex();
    }
}
