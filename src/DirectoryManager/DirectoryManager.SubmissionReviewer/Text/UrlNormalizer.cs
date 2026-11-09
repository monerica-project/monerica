using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Turns a URL into a comparison key so the same site is recognized however it was typed:
    /// scheme, "www.", case, trailing slash, query string and fragment are ignored.
    /// "https://www.Example.com/shop/?ref=1" and "http://example.com/shop" both become "example.com/shop".
    /// </summary>
    public static class UrlNormalizer
    {
        public static string Normalize(string? url)
        {
            var s = (url ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return string.Empty;
            }

            if (!s.Contains("://", StringComparison.Ordinal))
            {
                s = "http://" + s;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                return s.ToLowerInvariant().TrimEnd('/');
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host[4..];
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            if (path.Length > 0 && Regex.IsMatch(path, "^/(index\\.(html?|php))?$", RegexOptions.IgnoreCase))
            {
                path = string.Empty;
            }

            return host + path.ToLowerInvariant();
        }

        /// <summary>The host without "www." ("shop.example.com"), or empty when the URL can't be parsed.</summary>
        public static string Host(string? url)
        {
            var n = Normalize(url);
            var slash = n.IndexOf('/');
            return slash < 0 ? n : n[..slash];
        }

        public static bool IsOnion(string? url) => Host(url).EndsWith(".onion", StringComparison.Ordinal);
    }
}
