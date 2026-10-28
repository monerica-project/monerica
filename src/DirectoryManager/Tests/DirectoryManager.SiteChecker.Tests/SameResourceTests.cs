using DirectoryManager.SiteChecker.Helpers;

namespace DirectoryManager.SiteChecker.Tests
{
    // A 404 only means "gone" when it's the requested page. A redirect to a different page that
    // 404s is a bot-block / error page (clearnet and Tor checkers both rely on this comparison).
    public class SameResourceTests
    {
        [Theory]
        [InlineData("https://example.com/shop", "http://www.example.com/shop/", true)]
        [InlineData("https://Example.com/Shop", "https://example.com/shop", true)]
        [InlineData("https://example.com/", "https://example.com", true)]
        [InlineData("https://example.com/blocked.php", "https://example.com/", false)] // bot-block page
        [InlineData("https://other.com/", "https://example.com/", false)]
        public void Compares_host_and_path_ignoring_scheme_www_and_trailing_slash(string a, string b, bool expected)
        {
            Assert.Equal(expected, WebPageChecker.SameResource(new Uri(a), new Uri(b)));
        }
    }
}
