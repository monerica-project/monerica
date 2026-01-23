using System.Text;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
{
    public class SiteMapHelper
    {
        private const int MaxPageSizeForSiteMap = 50000;

        // Keep insertion order stable while enforcing uniqueness
        private readonly List<SiteMapItem> items = new ();
        private readonly Dictionary<string, int> urlIndexByKey = new (StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SiteMapItem> SiteMapItems => this.items;

        public void AddUrl(string url, DateTime lastMod, ChangeFrequency changeFrequency, double priority)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            // Key used ONLY for uniqueness comparisons
            var key = NormalizeUrlKey(url);

            // If already exists: update metadata (keep the original Url string as first added)
            if (this.urlIndexByKey.TryGetValue(key, out var idx))
            {
                var existing = this.items[idx];

                // Keep the most recent lastmod
                if (lastMod > existing.LastMod)
                {
                    existing.LastMod = lastMod;
                }

                // Prefer "more frequently changing" if you want; otherwise overwrite
                // Here: just overwrite
                existing.ChangeFrequency = changeFrequency;

                // Keep the higher priority (or overwrite—your choice)
                existing.Priority = Math.Max(existing.Priority, priority);

                this.items[idx] = existing;
                return;
            }

            // New item
            this.urlIndexByKey[key] = this.items.Count;
            this.items.Add(new SiteMapItem
            {
                Url = url,
                LastMod = lastMod,
                ChangeFrequency = changeFrequency,
                Priority = priority
            });
        }

        public string GenerateXml()
        {
            var sb = new StringBuilder();
            sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine();
            sb.AppendLine(@"<urlset xmlns=""https://www.sitemaps.org/schemas/sitemap/0.9"" xmlns:image=""https://www.google.com/schemas/sitemap-image/1.1"">");

            foreach (var siteMapItem in this.items.Take(MaxPageSizeForSiteMap))
            {
                sb.AppendLine(@"  <url>");
                sb.AppendFormat(@"    <loc>{0}</loc>", EscapeXml(siteMapItem.Url));
                sb.AppendLine();
                sb.AppendFormat(@"    <lastmod>{0}</lastmod>", siteMapItem.LastMod.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeFormatSiteMapXml));
                sb.AppendLine();
                sb.AppendFormat(@"    <changefreq>{0}</changefreq>", siteMapItem.ChangeFrequency.ToString());
                sb.AppendLine();
                sb.AppendFormat(@"    <priority>{0}</priority>", Math.Round(siteMapItem.Priority, 2));
                sb.AppendLine();
                sb.AppendLine(@"  </url>");
            }

            sb.AppendLine(@"</urlset>");
            return sb.ToString();
        }

        private static string NormalizeUrlKey(string url)
        {
            url = url.Trim();

            // If absolute, normalize via Uri
            if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
            {
                // drop fragment, normalize host casing, normalize path trailing slash
                var builder = new UriBuilder(abs) { Fragment = "" };

                var normalized = builder.Uri.ToString();

                // Remove trailing slash (except root "/")
                if (normalized.EndsWith("/", StringComparison.Ordinal) && builder.Path != "/")
                {
                    normalized = normalized.TrimEnd('/');
                }

                return normalized;
            }

            // If relative, normalize as path-only key
            // Remove fragment
            var hashIdx = url.IndexOf('#');
            if (hashIdx >= 0)
            {
                url = url[..hashIdx];
            }

            // Normalize leading slash
            if (!url.StartsWith("/"))
            {
                url = "/" + url;
            }

            // Remove trailing slash unless root
            if (url.Length > 1 && url.EndsWith("/", StringComparison.Ordinal))
            {
                url = url.TrimEnd('/');
            }

            return url;
        }

        private static string EscapeXml(string value)
        {
            // Minimal XML escaping for loc text content
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}