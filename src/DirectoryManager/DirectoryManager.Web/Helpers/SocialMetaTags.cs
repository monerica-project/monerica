using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds the description, Open Graph and Twitter card tags every page gets, and reconciles
    /// them with any raw &lt;meta&gt; markup a page supplies (the homepage's meta-tags snippet):
    /// values in the raw markup are used as fallbacks (image, twitter:site, description), and the
    /// raw copies of tags we generate are removed so no tag is emitted twice.
    /// </summary>
    public static class SocialMetaTags
    {
        /// <summary>Tag keys (name= or property=) this helper owns.</summary>
        public static readonly IReadOnlySet<string> GeneratedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "description",
            "og:title",
            "og:description",
            "og:url",
            "og:type",
            "og:site_name",
            "og:image",
            "twitter:card",
            "twitter:title",
            "twitter:description",
            "twitter:image",
            "twitter:site",
        };

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        private static readonly Regex MetaTagRegex = new (
            "<meta\\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        private static readonly Regex KeyRegex = new (
            "\\b(?:name|property)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        private static readonly Regex ContentRegex = new (
            "\\bcontent\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        /// <summary>Reads name/property → content from raw meta markup (first occurrence wins).</summary>
        public static Dictionary<string, string> Parse(string? html)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(html))
            {
                return result;
            }

            foreach (Match tag in MetaTagRegex.Matches(html))
            {
                var key = Attr(KeyRegex, tag.Value);
                var content = Attr(ContentRegex, tag.Value);
                if (key != null && content != null && !result.ContainsKey(key))
                {
                    result[key] = WebUtility.HtmlDecode(content).Trim();
                }
            }

            return result;
        }

        /// <summary>Raw markup with every &lt;meta&gt; whose key we generate removed.</summary>
        public static string RemoveGenerated(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var stripped = MetaTagRegex.Replace(html, m =>
            {
                var key = Attr(KeyRegex, m.Value);
                return key != null && GeneratedKeys.Contains(key) ? string.Empty : m.Value;
            });

            // Drop the blank lines the removed tags leave behind.
            var lines = stripped
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// The tags to emit, in order, as (attribute, key, value): attribute is "name" or "property".
        /// Empty values are skipped. twitter:card is "summary_large_image" when there is an image,
        /// otherwise "summary".
        /// </summary>
        public static List<(string Attribute, string Key, string Value)> Build(
            string? title,
            string? description,
            string? canonicalUrl,
            string? ogType,
            string? siteName,
            string? imageUrl,
            string? rawMetaContent)
        {
            var raw = Parse(rawMetaContent);

            string? FromRaw(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (raw.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    {
                        return v;
                    }
                }

                return null;
            }

            var desc = !string.IsNullOrWhiteSpace(description) ? description.Trim() : FromRaw("description", "og:description", "twitter:description");
            var image = !string.IsNullOrWhiteSpace(imageUrl) ? imageUrl.Trim() : FromRaw("og:image", "twitter:image");
            var twitterSite = NormalizeTwitterHandle(FromRaw("twitter:site"));
            var type = string.IsNullOrWhiteSpace(ogType) ? "website" : ogType.Trim();

            var tags = new List<(string Attribute, string Key, string Value)>();

            void Add(string attribute, string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    tags.Add((attribute, key, value.Trim()));
                }
            }

            Add("name", "description", desc);
            Add("property", "og:title", title);
            Add("property", "og:description", desc);
            Add("property", "og:url", canonicalUrl);
            Add("property", "og:type", type);
            Add("property", "og:site_name", siteName);
            Add("property", "og:image", image);
            Add("name", "twitter:card", string.IsNullOrWhiteSpace(image) ? "summary" : "summary_large_image");
            Add("name", "twitter:site", twitterSite);
            Add("name", "twitter:title", title);
            Add("name", "twitter:description", desc);
            Add("name", "twitter:image", image);

            return tags;
        }

        /// <summary>"MonericaProject" → "@MonericaProject"; blank stays blank.</summary>
        public static string? NormalizeTwitterHandle(string? handle)
        {
            if (string.IsNullOrWhiteSpace(handle))
            {
                return null;
            }

            var h = handle.Trim();
            return h.StartsWith('@') ? h : "@" + h;
        }

        private static string? Attr(Regex regex, string tag)
        {
            var m = regex.Match(tag);
            if (!m.Success)
            {
                return null;
            }

            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }
    }
}
