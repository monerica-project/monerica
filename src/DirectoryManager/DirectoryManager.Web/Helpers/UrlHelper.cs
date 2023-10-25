using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    public class UrlHelper
    {
        private static readonly Regex TorRegex =
            new Regex(
                @"^http[s]?://[a-z2-7]{16}\.onion(/.*)?$",
                RegexOptions.Compiled |
                RegexOptions.IgnoreCase);

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Check if it's a valid .onion link
            if (TorRegex.IsMatch(url))
            {
                return true;
            }

            // Check if it's a valid regular URL
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static string GetCurrentDomain(HttpContext httpContext)
        {
            return string.Format(
                "{0}{1}{2}",
                httpContext.Request.Scheme,
                "://",
                httpContext.Request.Host.ToUriComponent());
        }
    }
}
