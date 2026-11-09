namespace DirectoryManager.Utilities.Helpers
{
    /// <summary>
    /// "No link twice" rule for listing forms: every link field (main link, Tor/I2P links, proof,
    /// video, source code, related links, and Social/Messenger when they hold a URL) must point
    /// somewhere different. One exception: the proof link may equal the main link (the profile
    /// then simply doesn't show it twice).
    /// </summary>
    public static class LinkUniqueness
    {
        public const string MainLinkField = "Link";
        public const string ProofLinkField = "ProofLink";

        /// <summary>
        /// Returns (field, message) for every field whose link already appears in an EARLIER field
        /// of <paramref name="fields"/> (so the first use stays, the repeat is flagged).
        /// </summary>
        public static IReadOnlyList<(string Field, string Message)> FindDuplicates(
            IEnumerable<(string Field, string Label, string? Value)> fields)
        {
            var seen = new List<(string Field, string Label, string Key)>();
            var errors = new List<(string Field, string Message)>();

            foreach (var (field, label, value) in fields)
            {
                var key = KeyFor(value);
                if (key == null)
                {
                    continue;
                }

                var earlier = seen.FirstOrDefault(s => s.Key == key
                    && !IsAllowedPair(s.Field, field));
                if (earlier.Key != null)
                {
                    errors.Add((field, $"{label} is the same link as {earlier.Label}. Each link must be different; remove it or use the right link."));
                    continue;
                }

                seen.Add((field, label, key));
            }

            return errors;
        }

        /// <summary>A comparable key for values that look like a web address; null for blanks, @handles, emails, etc.</summary>
        public static string? KeyFor(string? value)
        {
            var v = value?.Trim();
            if (string.IsNullOrEmpty(v) || v.Any(char.IsWhiteSpace) || v.StartsWith('@') || v.Contains('@') && !v.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            if (!v.Contains("://", StringComparison.Ordinal) && !v.Contains('.'))
            {
                return null;
            }

            return UrlHelper.LinkKey(v);
        }

        private static bool IsAllowedPair(string a, string b) =>
            (a == MainLinkField && b == ProofLinkField) || (a == ProofLinkField && b == MainLinkField);
    }
}
