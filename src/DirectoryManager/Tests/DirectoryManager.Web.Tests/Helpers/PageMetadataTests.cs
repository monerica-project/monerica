using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Http;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class PageMetadataTests
    {
        [Fact]
        public void For_ReturnsTheSameInstanceForOneRequest()
        {
            var ctx = new DefaultHttpContext();

            var first = PageMetadata.For(ctx);
            first.Title = "Hello";

            Assert.Same(first, PageMetadata.For(ctx));
            Assert.Equal("Hello", PageMetadata.For(ctx).Title);
        }

        [Fact]
        public void For_SeparateRequestsDoNotShare()
        {
            PageMetadata.For(new DefaultHttpContext()).Title = "A";

            Assert.Null(PageMetadata.For(new DefaultHttpContext()).Title);
        }

        [Fact]
        public void For_NullContext_ReturnsUsableThrowawayInstance()
        {
            Assert.NotNull(PageMetadata.For(null));
        }

        [Fact]
        public void EntryHtml_ShowsProfileLink_OnlyWhenRequested()
        {
            var vm = new DirectoryEntryViewModel
            {
                Name = "Example",
                DirectoryEntryKey = "example",
                Link = "https://example.com",
                ItemPath = "/site/example",
                Description = "Desc",
            };

            Assert.DoesNotContain("See profile", DisplayMarkUpHelper.GenerateDirectoryEntryHtml(vm, "https://dir.example"));

            vm.ShowProfileLink = true;
            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(vm, "https://dir.example");
            Assert.Contains("See profile", html);
            Assert.Contains("https://dir.example/site/example", html);
        }
    }
}
