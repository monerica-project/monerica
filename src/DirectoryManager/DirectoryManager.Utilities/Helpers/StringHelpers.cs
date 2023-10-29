using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Helpers
{
    public class StringHelpers
    {
        public static string UrlKey(string p)
        {
            var replaceRegex = Regex.Replace(p, @"[\W_-[#]]+", " ");

            var beforeTrim = replaceRegex.Trim()
                                         .Replace("  ", " ")
                                         .Replace(" ", "-")
                                         .Replace("%", string.Empty)
                                         .Replace("&", "and")
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
    }
}