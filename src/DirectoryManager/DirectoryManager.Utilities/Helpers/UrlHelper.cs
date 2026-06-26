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

        /// <summary>
        /// True when the URL points at a Tor (.onion) or I2P (.i2p / .b32.i2p)
        /// hidden service. The main directory Link must be clearnet; hidden-service
        /// addresses belong in the alternate link fields.
        /// </summary>
        public static bool IsOnionOrI2p(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var host = url.Trim();
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }

            host = host.ToLowerInvariant();
            return host.EndsWith(".onion", StringComparison.Ordinal)
                   || host.EndsWith(".i2p", StringComparison.Ordinal);
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));
            }

            // Remove multiple slashes (except for the protocol part)
            string normalizedUrl = Regex.Replace(url, "(?<!:)/{2,}", "/");

            // Remove the ending slash if present
            if (normalizedUrl.EndsWith("/"))
            {
                normalizedUrl = normalizedUrl.TrimEnd('/');
            }

            return normalizedUrl;
        }

        public static string MakeFullUrl(string domain, string relativePath)
        {
            // ensure the domain has no trailing slash before building
            var baseUri = new Uri(domain.TrimEnd('/'), UriKind.Absolute);

            // combine domain + path
            var fullUri = new Uri(baseUri, relativePath);

            // AbsoluteUri gives you the normalized URL, then TrimEnd removes any trailing slash
            return fullUri.AbsoluteUri.TrimEnd('/');
        }
    }
}