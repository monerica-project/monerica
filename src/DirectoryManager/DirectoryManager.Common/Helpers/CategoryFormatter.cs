namespace DirectoryManager.Common.Helpers
{
    /// <summary>
    /// Single source of truth for rendering the "Category » Subcategory" path.
    /// Always use this (never a hand-written "Cat > Sub") so the separator is
    /// consistent everywhere and never leaves a bare "&gt;"/"&amp;" in the output.
    /// <see cref="DirectoryManager.DisplayFormatting.Helpers.FormattingHelper.SubcategoryFormatting"/>
    /// delegates here so display code and the data layer share one definition.
    /// </summary>
    public static class CategoryFormatter
    {
        /// <summary>
        /// The separator shown between a category and its subcategory. A real
        /// U+00BB "»" (not "&gt;") — it survives a single HTML-encode as "&#187;"
        /// and renders identically in native select options and Razor output.
        /// </summary>
        public const string Separator = " » ";

        /// <summary>
        /// Builds a raw (un-encoded) "Category » Subcategory" string, gracefully
        /// collapsing to whichever side is present when one is blank. Callers that
        /// place the result into HTML should HTML-encode it exactly once.
        /// </summary>
        public static string Format(string? categoryName, string? subcategoryName)
        {
            var hasCategory = !string.IsNullOrWhiteSpace(categoryName);
            var hasSubcategory = !string.IsNullOrWhiteSpace(subcategoryName);

            if (!hasCategory && !hasSubcategory)
            {
                return string.Empty;
            }

            if (!hasCategory)
            {
                return subcategoryName!;
            }

            if (!hasSubcategory)
            {
                return categoryName!;
            }

            return string.Concat(categoryName, Separator, subcategoryName);
        }
    }
}
