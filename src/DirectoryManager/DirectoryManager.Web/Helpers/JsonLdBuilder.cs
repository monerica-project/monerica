using System.Text.Json;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds schema.org JSON-LD documents as strings for <see cref="PageMetadata.StructuredData"/>.
    /// The default System.Text.Json encoder escapes &lt;, &gt;, &amp; and quotes, so the output is
    /// safe to place inside a &lt;script&gt; element as is.
    /// </summary>
    public static class JsonLdBuilder
    {
        private const string Context = "https://schema.org";

        private static readonly JsonSerializerOptions Options = new ()
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            WriteIndented = false,
        };

        /// <summary>
        /// WebSite node. <paramref name="searchUrlTemplate"/> must contain "{search_term_string}";
        /// when blank, no SearchAction is added.
        /// </summary>
        public static string WebSite(string siteName, string url, string? description, string? searchUrlTemplate)
        {
            var node = new Dictionary<string, object>
            {
                ["@context"] = Context,
                ["@type"] = "WebSite",
                ["name"] = siteName,
                ["url"] = url,
            };

            if (!string.IsNullOrWhiteSpace(description))
            {
                node["description"] = description;
            }

            if (!string.IsNullOrWhiteSpace(searchUrlTemplate)
                && searchUrlTemplate.Contains("{search_term_string}", StringComparison.Ordinal))
            {
                node["potentialAction"] = new Dictionary<string, object>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = new Dictionary<string, object>
                    {
                        ["@type"] = "EntryPoint",
                        ["urlTemplate"] = searchUrlTemplate,
                    },
                    ["query-input"] = "required name=search_term_string",
                };
            }

            return Serialize(node);
        }

        /// <summary>Organization node; logo and sameAs are only added when known.</summary>
        public static string Organization(string siteName, string url, string? logoUrl, IEnumerable<string>? sameAs)
        {
            var node = new Dictionary<string, object>
            {
                ["@context"] = Context,
                ["@type"] = "Organization",
                ["name"] = siteName,
                ["url"] = url,
            };

            if (!string.IsNullOrWhiteSpace(logoUrl))
            {
                node["logo"] = logoUrl;
            }

            var links = (sameAs ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s)
                         && Uri.TryCreate(s, UriKind.Absolute, out var u)
                         && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (links.Count > 0)
            {
                node["sameAs"] = links;
            }

            return Serialize(node);
        }

        /// <summary>ItemList of links (position starts at <paramref name="startPosition"/>).</summary>
        public static string ItemList(string name, string? url, IEnumerable<(string Name, string Url)> items, int startPosition = 1)
        {
            var list = (items ?? Enumerable.Empty<(string Name, string Url)>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Name) && !string.IsNullOrWhiteSpace(i.Url))
                .Select((i, idx) => new Dictionary<string, object>
                {
                    ["@type"] = "ListItem",
                    ["position"] = startPosition + idx,
                    ["name"] = i.Name,
                    ["url"] = i.Url,
                })
                .ToList();

            var node = new Dictionary<string, object>
            {
                ["@context"] = Context,
                ["@type"] = "ItemList",
                ["name"] = name,
                ["numberOfItems"] = list.Count,
                ["itemListElement"] = list,
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                node["url"] = url;
            }

            return Serialize(node);
        }

        /// <summary>BreadcrumbList; items with a blank name or URL are skipped and positions renumbered.</summary>
        public static string BreadcrumbList(IEnumerable<BreadcrumbItem> items)
        {
            var list = (items ?? Enumerable.Empty<BreadcrumbItem>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Name) && !string.IsNullOrWhiteSpace(i.Url))
                .Select((i, idx) => new Dictionary<string, object>
                {
                    ["@type"] = "ListItem",
                    ["position"] = idx + 1,
                    ["name"] = i.Name!,
                    ["item"] = i.Url!,
                })
                .ToList();

            return Serialize(new Dictionary<string, object>
            {
                ["@context"] = Context,
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = list,
            });
        }

        private static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
    }
}
