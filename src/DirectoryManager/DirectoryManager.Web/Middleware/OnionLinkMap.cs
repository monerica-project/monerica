using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Middleware
{
    /// <summary>
    /// For visitors on the Tor (.onion) version of the site: links to our other sites that have
    /// their own onion address (the blog, the video site) are pointed at that onion address, so a
    /// Tor visitor is not sent out to the clearnet.
    ///
    /// The pairs come from the "OnionLinkMap" site setting, one per line:
    ///   blog.example.com = abcdefghijklmnop.onion
    /// Lines that are empty, start with # or are not a plain host and an .onion host are ignored.
    /// </summary>
    public static partial class OnionLinkMap
    {
        /// <summary>Reads the setting text into (clearnet host, onion host) pairs.</summary>
        public static IReadOnlyList<(string Host, string Onion)> Parse(string? settingText)
        {
            var pairs = new List<(string, string)>();
            foreach (var raw in (settingText ?? string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var host = StripScheme(parts[0]);
                var onion = StripScheme(parts[1]);
                if (HostPattern().IsMatch(host) && !host.EndsWith(".onion", StringComparison.Ordinal)
                    && HostPattern().IsMatch(onion) && onion.EndsWith(".onion", StringComparison.Ordinal))
                {
                    pairs.Add((host, onion));
                }
            }

            return pairs;
        }

        /// <summary>Points every http(s) link to a mapped host at its onion address (always http).</summary>
        public static string Rewrite(string html, IReadOnlyList<(string Host, string Onion)> pairs)
        {
            if (pairs.Count == 0 || string.IsNullOrEmpty(html))
            {
                return html;
            }

            foreach (var (host, onion) in pairs)
            {
                if (!html.Contains(host, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // The host must end there: "blog.example.com" must not match "blog.example.com.evil.net".
                html = Regex.Replace(
                    html,
                    @"https?://" + Regex.Escape(host) + @"(?![A-Za-z0-9.\-])",
                    "http://" + onion,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(2));
            }

            return html;
        }

        private static string StripScheme(string value)
        {
            var v = value.Trim().ToLowerInvariant();
            foreach (var scheme in new[] { "https://", "http://" })
            {
                if (v.StartsWith(scheme, StringComparison.Ordinal))
                {
                    v = v[scheme.Length..];
                }
            }

            return v.TrimEnd('/');
        }

        [GeneratedRegex(@"^[a-z0-9]([a-z0-9\-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9\-]*[a-z0-9])?)+$")]
        private static partial Regex HostPattern();
    }
}
