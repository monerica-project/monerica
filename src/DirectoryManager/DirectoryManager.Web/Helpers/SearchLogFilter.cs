using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Decides whether a public search term is worth keeping in the search log. Probe-shaped
    /// and machine-made terms are still answered but not logged, so the admin search report
    /// shows what people look for. Found in the log (120 days to 2026-11-13): 413 lone-quote
    /// probes in one scanner burst ("test'", "test\""), 52 unfilled search templates
    /// ("{searchTerms}", "{search_term_string}"), 27 bare hashes, 17 punctuation-only and 21
    /// single-character terms. Uses only the term itself (no IP or other visitor data).
    /// </summary>
    public static class SearchLogFilter
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

        private static readonly Regex LoneQuoteProbe = new (
            @"^(?:[\p{L}\p{N}]+['""`]+|['""`]+[\p{L}\p{N}]+)$",
            RegexOptions.CultureInvariant,
            Timeout);

        private static readonly Regex HexHash = new (@"^[0-9a-fA-F]{32,}$", RegexOptions.CultureInvariant, Timeout);

        public static bool ShouldLog(string? term)
        {
            var t = (term ?? string.Empty).Trim();
            if (t.Length < 2 || t.Contains('{') || t.Contains('}') || !t.Any(char.IsLetterOrDigit))
            {
                return false;
            }

            try
            {
                return !LoneQuoteProbe.IsMatch(t) && !HexHash.IsMatch(t);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
