using System.Text.Json;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Seo
{
    public class JsonLdBuilderTests
    {
        [Fact]
        public void WebSite_HasSearchActionOnlyWithTemplate()
        {
            using var with = JsonDocument.Parse(JsonLdBuilder.WebSite("Site", "https://e.com/", "Desc", "https://app.e.com/search?q={search_term_string}"));
            Assert.Equal("WebSite", with.RootElement.GetProperty("@type").GetString());
            Assert.Equal(
                "https://app.e.com/search?q={search_term_string}",
                with.RootElement.GetProperty("potentialAction").GetProperty("target").GetProperty("urlTemplate").GetString());

            using var without = JsonDocument.Parse(JsonLdBuilder.WebSite("Site", "https://e.com/", null, null));
            Assert.False(without.RootElement.TryGetProperty("potentialAction", out _));
        }

        [Fact]
        public void Organization_FiltersInvalidSameAs()
        {
            using var doc = JsonDocument.Parse(JsonLdBuilder.Organization(
                "Site", "https://e.com/", "https://e.com/logo.png", new[] { "https://x.com/a", "not a url", "javascript:alert(1)", "https://x.com/a" }));
            var sameAs = doc.RootElement.GetProperty("sameAs").EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Equal(new[] { "https://x.com/a" }, sameAs);
            Assert.Equal("https://e.com/logo.png", doc.RootElement.GetProperty("logo").GetString());
        }

        [Fact]
        public void ItemList_NumbersFromStartPosition()
        {
            using var doc = JsonDocument.Parse(JsonLdBuilder.ItemList(
                "Wallets", "https://e.com/wallets", new[] { ("A", "https://e.com/site/a"), ("B", "https://e.com/site/b") }, 51));
            var items = doc.RootElement.GetProperty("itemListElement").EnumerateArray().ToList();
            Assert.Equal(51, items[0].GetProperty("position").GetInt32());
            Assert.Equal(52, items[1].GetProperty("position").GetInt32());
            Assert.Equal(2, doc.RootElement.GetProperty("numberOfItems").GetInt32());
        }

        [Fact]
        public void Breadcrumb_SkipsBlankItemsAndRenumbers()
        {
            using var doc = JsonDocument.Parse(JsonLdBuilder.BreadcrumbList(new[]
            {
                new BreadcrumbItem { Position = 1, Name = "Home", Url = "https://e.com/" },
                new BreadcrumbItem { Position = 2, Name = string.Empty, Url = "https://e.com/x" },
                new BreadcrumbItem { Position = 3, Name = "Listing", Url = "https://e.com/site/l" },
            }));
            var items = doc.RootElement.GetProperty("itemListElement").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal(2, items[1].GetProperty("position").GetInt32());
        }

        [Fact]
        public void Output_IsSafeInsideScriptElement()
        {
            var json = JsonLdBuilder.ItemList("</script><script>alert(1)</script>", null, new[] { ("a&b", "https://e.com/") });
            Assert.DoesNotContain("</script>", json);
            Assert.DoesNotContain("<", json);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("</script><script>alert(1)</script>", doc.RootElement.GetProperty("name").GetString());
        }
    }
}
