using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    // Links straight to programs/archives are refused; the same link is matched however it is typed.
    public class SubmissionLinkRulesTests
    {
        [Theory]
        [InlineData("https://example.com/download/setup.exe", "exe")]
        [InlineData("https://example.com/app.APK", "apk")]
        [InlineData("https://files.example.com/wallet.tar.gz", "gz")]
        [InlineData("https://example.com/files/bundle.zip?ref=1", "zip")]
        [InlineData("http://abcdefghijklmnop.onion/get/tool.dmg", "dmg")]
        [InlineData("https://example.com/install.sh/", "sh")]
        [InlineData("https://example.com/setup%2Eexe", "exe")]
        [InlineData("example.com/run.msi", "msi")]
        public void FileLinks_AreDetected(string url, string expected)
        {
            Assert.Equal(expected, UrlHelper.BlockedFileExtension(url));
        }

        [Theory]
        [InlineData("https://example.com")]
        [InlineData("https://example.com/")]
        [InlineData("https://shop.example.com/index.html")]
        [InlineData("https://example.com/page.php?file=setup.exe")]
        [InlineData("https://example.com/docs#setup.exe")]
        [InlineData("https://example.zip")]
        [InlineData("https://example.com/v1.2")]
        [InlineData("https://example.com/.well-known")]
        [InlineData("not a url")]
        [InlineData(null)]
        [InlineData("")]
        public void WebPages_AreAllowed(string? url)
        {
            Assert.Null(UrlHelper.BlockedFileExtension(url));
        }

        [Theory]
        [InlineData("https://example.com/shop", "http://www.Example.com/shop/")]
        [InlineData("https://example.com", "example.com")]
        [InlineData("https://example.com/a?x=1", "https://example.com/a?x=1#top")]
        public void SameLink_IgnoresSchemeWwwCaseSlashAndFragment(string a, string b)
        {
            Assert.True(UrlHelper.SameLink(a, b));
        }

        [Theory]
        [InlineData("https://example.com/shop", "https://example.com/other")]
        [InlineData("https://example.com", "https://pay.example.com")]
        [InlineData("https://example.com", "https://evilexample.com")]
        [InlineData("https://example.com/a?x=1", "https://example.com/a?x=2")]
        [InlineData("https://example.com", null)]
        [InlineData(null, null)]
        public void SameLink_DifferentLinks_DoNotMatch(string? a, string? b)
        {
            Assert.False(UrlHelper.SameLink(a, b));
        }
    }
}
