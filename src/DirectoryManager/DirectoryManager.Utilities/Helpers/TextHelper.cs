// DirectoryManager.Utilities/Helpers/TextHelper.cs
namespace DirectoryManager.Utilities.Helpers
{
    public static class TextHelper
    {
        /// <summary>
        /// Truncates to the nearest word at or before maxLength and appends an ellipsis if truncated.
        /// Preserves empty/null safely.
        /// </summary>
        /// <returns>The value returned with dots.</returns>
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

            // simple + effective checks
            return t.Contains("http://")
                || t.Contains("https://")
                || t.Contains("www.")
                || t.Contains(".com")
                || t.Contains(".net")
                || t.Contains(".org")
                || t.Contains(".onion");
        }
    }
}