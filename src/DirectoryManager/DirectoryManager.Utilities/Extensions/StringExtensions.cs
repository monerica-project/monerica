using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities
{
    public static class StringExtensions
    {
        public static string UrlKey(this string p)
        {
            var replaceRegex = Regex.Replace(p, @"[\W_-[#]]+", " ");

            var beforeTrim = replaceRegex.Trim()
                                         .Replace("  ", " ")
                                         .Replace(" ", "-")
                                         .Replace("%", string.Empty)
                                         .ToLowerInvariant();

            if (beforeTrim.EndsWith("#"))
            {
                beforeTrim = beforeTrim.TrimEnd('#');
            }

            if (beforeTrim.StartsWith("#"))
            {
                beforeTrim = beforeTrim.TrimStart('#');
            }

            return beforeTrim;
        }

        public static string GetFileNameFromUrl(this string url)
        {
            Uri uri = new Uri(url);

            string filename = Path.GetFileName(uri.LocalPath);

            return filename;
        }

        public static string GetFileExtensionLower(this string fileName)
        {
            return Path.GetExtension(fileName).ToLower()
                                              .Replace(".", string.Empty);
        }

        public static string GetFileExtension(this string fileName)
        {
            return Path.GetExtension(fileName).Replace(".", string.Empty);
        }
    }
}