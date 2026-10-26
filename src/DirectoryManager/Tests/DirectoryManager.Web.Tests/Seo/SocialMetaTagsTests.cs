using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Seo
{
    public class SocialMetaTagsTests
    {
        // The shape of the homepage meta-tags snippet in production.
        private const string HomeSnippet =
            "    <meta name=\"description\" content=\"A directory &amp; more.\" />\n" +
            "    <meta name=\"twitter:card\" content=\"A whole sentence that is not a card type.\" />\n" +
            "    <meta name=\"twitter:site\" content=\"MonericaProject\" />\n" +
            "    <meta name=\"twitter:title\" content=\"Monerica\" />\n" +
            "    <meta property=\"og:image\" content=\"https://example.com/logo.png\" />\n" +
            "    <meta name=\"robots\" content=\"noindex\">";

        [Fact]
        public void Parse_ReadsNameAndPropertyTags()
        {
            var tags = SocialMetaTags.Parse(HomeSnippet);
            Assert.Equal("A directory & more.", tags["description"]);
            Assert.Equal("https://example.com/logo.png", tags["og:image"]);
            Assert.Equal("MonericaProject", tags["twitter:site"]);
        }

        [Fact]
        public void RemoveGenerated_KeepsOnlyTagsWeDoNotGenerate()
        {
            var rest = SocialMetaTags.RemoveGenerated(HomeSnippet);
            Assert.Equal("    <meta name=\"robots\" content=\"noindex\">", rest);
        }

        [Fact]
        public void Build_UsesValidCardTypeAndSnippetFallbacks()
        {
            var tags = SocialMetaTags.Build("Title", null, "https://example.com/", null, "Site", null, HomeSnippet)
                .ToDictionary(t => t.Key, t => t.Value);

            Assert.Equal("summary_large_image", tags["twitter:card"]);
            Assert.Equal("A directory & more.", tags["description"]);
            Assert.Equal("A directory & more.", tags["og:description"]);
            Assert.Equal("https://example.com/logo.png", tags["og:image"]);
            Assert.Equal("@MonericaProject", tags["twitter:site"]);
            Assert.Equal("website", tags["og:type"]);
            Assert.Equal("Title", tags["og:title"]);
            Assert.Equal("Title", tags["twitter:title"]);
            Assert.Equal("https://example.com/", tags["og:url"]);
            Assert.Equal("Site", tags["og:site_name"]);
        }

        [Fact]
        public void Build_WithoutImageIsSummaryCard()
        {
            var tags = SocialMetaTags.Build("T", "D", null, null, "S", null, null);
            Assert.Contains(tags, t => t.Key == "twitter:card" && t.Value == "summary");
            Assert.DoesNotContain(tags, t => t.Key == "og:image" || t.Key == "og:url");
        }

        [Fact]
        public void Build_PageValuesWinOverSnippet()
        {
            var tags = SocialMetaTags.Build("T", "Page description", null, "article", "S", "https://example.com/page.png", HomeSnippet)
                .ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("Page description", tags["description"]);
            Assert.Equal("https://example.com/page.png", tags["og:image"]);
            Assert.Equal("article", tags["og:type"]);
        }

        [Fact]
        public void Build_EmitsEachKeyOnce()
        {
            var tags = SocialMetaTags.Build("T", "D", "https://e.com/", null, "S", "https://e.com/i.png", HomeSnippet);
            Assert.Equal(tags.Count, tags.Select(t => t.Key).Distinct().Count());
        }
    }
}
