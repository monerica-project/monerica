using System.Text;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
{
    public class SiteMapHelper
    {
        public List<SiteMapItem> SiteMapItems { get; set; } = new List<SiteMapItem>();

        public void AddUrl(string url, DateTime lastMod, ChangeFrequency changeFrequency, double priority)
        {
            this.SiteMapItems.Add(new SiteMapItem
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
            sb.AppendLine(@"<urlset xmlns=""https://www.sitemaps.org/schemas/sitemap/0.9"" xmlns:image=""https://www.google.com/schemas/sitemap-image/1.1"">");

            foreach (var siteMapItem in this.SiteMapItems)
            {
                sb.AppendLine(@"<url>");
                sb.AppendFormat(@"<loc>{0}</loc>", siteMapItem.Url);
                sb.AppendFormat(@"<lastmod>{0}</lastmod>", siteMapItem.LastMod.ToString("yyyy-MM-dd"));
                sb.AppendFormat(@"<changefreq>{0}</changefreq>", siteMapItem.ChangeFrequency.ToString());
                sb.AppendFormat(@"<priority>{0}</priority>", Math.Round(siteMapItem.Priority, 2));
                sb.AppendLine();
                sb.AppendLine(@"</url>");
            }

            sb.AppendLine(@"</urlset>");

            return sb.ToString();
        }
    }
}
