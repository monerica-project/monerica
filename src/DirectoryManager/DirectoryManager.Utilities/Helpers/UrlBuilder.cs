namespace DirectoryManager.Utilities.Helpers
{
    public class UrlBuilder
    {
        public static string BlogUrlPath(string sectionKey, string pageKey)
        {
            return string.Format("/{0}/{1}", sectionKey, pageKey);
        }

        public static string BlogPreviewUrlPath(int sitePageId)
        {
            return string.Format(
                "/preview/{0}",
                sitePageId);
        }

        public static string CombineUrl(string domain, string path)
        {
            // Trim any trailing slashes from the domain and leading slashes from the path
            domain = domain.TrimEnd('/');
            path = path.TrimStart('/');

            // Combine the domain and the path with a single slash
            return $"{domain}/{path}";
        }

        public static string ConvertBlobToCdnUrl(
           string blobUrl,
           string blobPrefix,
           string cdnPrefix)
        {
            if (string.IsNullOrWhiteSpace(blobUrl))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(cdnPrefix) ||
                string.IsNullOrWhiteSpace(blobPrefix))
            {
                return string.Empty;
            }

            return blobUrl.Replace(blobPrefix, cdnPrefix);
        }
    }
}
