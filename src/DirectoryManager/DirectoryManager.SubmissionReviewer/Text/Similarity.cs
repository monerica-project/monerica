namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>Trigram (Jaccard) similarity, 0..1, used to find existing listings that look like a submission.</summary>
    public static class Similarity
    {
        public static double Trigram(string? a, string? b)
        {
            var ta = Grams(a);
            var tb = Grams(b);
            if (ta.Count == 0 || tb.Count == 0)
            {
                return 0;
            }

            var inter = ta.Count(tb.Contains);
            return (double)inter / (ta.Count + tb.Count - inter);
        }

        private static HashSet<string> Grams(string? s)
        {
            var clean = new string((s ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (clean.Length == 0)
            {
                return set;
            }

            var padded = "  " + clean + " ";
            for (int i = 0; i + 3 <= padded.Length; i++)
            {
                set.Add(padded.Substring(i, 3));
            }

            return set;
        }
    }
}
