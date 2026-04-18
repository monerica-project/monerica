using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Utilities
{
    public static class StringExtensions
    {
        public static string UrlKey(this string p)
        {
            return StringHelpers.UrlKey(p);
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