namespace DirectoryManager.Web.Helpers
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
    }
}