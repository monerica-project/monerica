using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Helpers
{
    public class StringHelpers
    {
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
                // Keep only base characters (e.g., 'e' instead of 'é').
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

            // Step 5: Use regex to replace non-alphanumeric characters with a single space.
            var replaceRegex = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s-]+", " ");

            // Step 6: Collapse multiple spaces or dashes into a single dash.
            var urlSafe = Regex.Replace(replaceRegex, @"[\s-]+", "-").Trim('-');

            // Step 7: Convert to lowercase.
            return urlSafe.ToLowerInvariant();
        }
    }
}