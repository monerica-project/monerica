using System.Text.RegularExpressions;
using Humanizer;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    public class FormattingHelper
    {
        public static string ListingPath(string directoryEntryKey)
        {
            return string.Format("/site/{0}", directoryEntryKey);
        }

        public static string SubcategoryFormatting(string? categoryName, string? subcategoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) && string.IsNullOrWhiteSpace(subcategoryName))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(categoryName) && !string.IsNullOrWhiteSpace(subcategoryName))
            {
                return subcategoryName;
            }

            if (!string.IsNullOrWhiteSpace(categoryName) && string.IsNullOrWhiteSpace(subcategoryName))
            {
                return categoryName;
            }

            return $"{categoryName} » {subcategoryName}";
        }

        public static string NormalizeTagName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // 1) normalize
            var clean = raw.Trim().ToLowerInvariant();

            // → if it contains any spaces, just return it verbatim (no singularizing)
            if (clean.Contains(' '))
            {
                return clean;
            }

            // 1a) if a short tag (3–4 chars) ends with “s”, leave the “s” intact
            if ((clean.Length == 3 || clean.Length == 4) && clean.EndsWith("s"))
            {
                return clean;
            }

            // 2) manual rule for common “-es” plurals:
            //    taxes→tax, boxes→box, churches→church, brushes→brush, etc.
            if (Regex.IsMatch(clean, "(ses|xes|zes|ches|shes)$"))
            {
                return clean.Substring(0, clean.Length - 2);
            }

            // 2b) don’t singularize adjectives ending in “ous”
            if (clean.EndsWith("ous"))
            {
                return clean;
            }

            // 3) fallback to Humanizer’s singularizer
            return clean.Singularize(false);
        }
    }
}