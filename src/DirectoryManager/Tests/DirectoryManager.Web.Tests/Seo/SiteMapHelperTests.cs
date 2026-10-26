using System.Xml.Linq;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Seo
{
    public class SiteMapHelperTests
    {
        [Fact]
        public void Xml_UsesSyncerNamespaceLowercaseChangefreqAndPerUrlLastmod()
        {
            var helper = new SiteMapHelper();
            helper.AddUrl("https://e.com/", new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), ChangeFrequency.Daily, 1.0);
            helper.AddUrl("https://e.com/site/a", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), ChangeFrequency.Weekly, 0.7);

            var doc = XDocument.Parse(helper.GenerateXml());
            // The standard sitemap protocol namespace.
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            Assert.Equal(SiteMapHelper.Namespace, ns.NamespaceName);

            Assert.Equal(ns, doc.Root!.Name.Namespace);
            Assert.DoesNotContain("sitemap-image", doc.ToString());

            var urls = doc.Root.Elements(ns + "url").ToList();
            Assert.Equal(2, urls.Count);
            Assert.Equal("daily", urls[0].Element(ns + "changefreq")!.Value);
            Assert.Equal("weekly", urls[1].Element(ns + "changefreq")!.Value);
            Assert.Equal("2026-10-24T12:00:00+00:00", urls[0].Element(ns + "lastmod")!.Value);
            Assert.Equal("2026-01-02T03:04:05+00:00", urls[1].Element(ns + "lastmod")!.Value);
            Assert.Equal("0.7", urls[1].Element(ns + "priority")!.Value);
        }

        [Fact]
        public void DuplicateUrl_KeepsNewestLastmod()
        {
            var helper = new SiteMapHelper();
            helper.AddUrl("https://e.com/x", new DateTime(2026, 1, 1), ChangeFrequency.Weekly, 0.4);
            helper.AddUrl("https://e.com/x/", new DateTime(2026, 2, 1), ChangeFrequency.Weekly, 0.6);

            Assert.Single(helper.SiteMapItems);
            Assert.Equal(new DateTime(2026, 2, 1), helper.SiteMapItems[0].LastMod);
        }

        [Theory]
        [InlineData(ChangeFrequency.Always, "always")]
        [InlineData(ChangeFrequency.Monthly, "monthly")]
        [InlineData(ChangeFrequency.Never, "never")]
        public void ChangeFrequency_IsLowercase(ChangeFrequency value, string expected)
        {
            Assert.Equal(expected, SiteMapHelper.ChangeFrequencyValue(value));
        }
    }
}
