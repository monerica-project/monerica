using System.Text.RegularExpressions;
using Humanizer;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    public class FormattingHelper
    {
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

            return $"{categoryName} > {subcategoryName}";
        }

        public static string NormalizeTagName(string raw)
        {

            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // 1) trim + lower
            var clean = raw.Trim().ToLowerInvariant();

            // 2) manual rule for common -es plurals
            //    taxes→tax, boxes→box, churches→church, brushes→brush, etc.
            if (Regex.IsMatch(clean, "(ses|xes|zes|ches|shes)$"))
            {
                return clean.Substring(0, clean.Length - 2);
            }

            // 3) fallback to Humanizer’s singularizer for everything else
            return clean.Singularize(false);
        }
    }
}