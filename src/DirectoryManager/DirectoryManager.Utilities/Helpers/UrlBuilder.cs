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
