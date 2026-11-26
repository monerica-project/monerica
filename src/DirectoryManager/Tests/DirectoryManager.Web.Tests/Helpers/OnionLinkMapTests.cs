using DirectoryManager.Web.Middleware;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class OnionLinkMapTests
    {
        private const string Setting = "# our other sites\nblog.example.com = blogonionaddress.onion\nhttps://video.example.com/ = http://videoonionaddress.onion\n\nbad line\nevil.onion = other.onion\nfoo.example.com = notanonion.com\n";

        [Fact]
        public void Parse_KeepsOnlyHostToOnionPairs()
        {
            var pairs = OnionLinkMap.Parse(Setting);
            Assert.Equal(
                new[] { ("blog.example.com", "blogonionaddress.onion"), ("video.example.com", "videoonionaddress.onion") },
                pairs);
            Assert.Empty(OnionLinkMap.Parse(null));
        }

        [Fact]
        public void Rewrite_PointsLinksAndVisibleAddressesAtTheOnion()
        {
            var html = "<a href=\"https://blog.example.com/reviews/x\">https://blog.example.com/reviews/x</a> <a href=\"http://BLOG.example.com\">blog</a> <a href=\"https://video.example.com/watch/1\">v</a>";
            var result = OnionLinkMap.Rewrite(html, OnionLinkMap.Parse(Setting));
            Assert.Equal(
                "<a href=\"http://blogonionaddress.onion/reviews/x\">http://blogonionaddress.onion/reviews/x</a> <a href=\"http://blogonionaddress.onion\">blog</a> <a href=\"http://videoonionaddress.onion/watch/1\">v</a>",
                result);
        }

        [Fact]
        public void Rewrite_LeavesOtherHostsAlone()
        {
            var html = "<a href=\"https://blog.example.com.evil.net/x\">a</a> <a href=\"https://notblog.example.org/\">b</a> <a href=\"https://myblog.example.com-phish.io\">c</a>";
            Assert.Equal(html, OnionLinkMap.Rewrite(html, OnionLinkMap.Parse(Setting)));
            Assert.Equal(html, OnionLinkMap.Rewrite(html, OnionLinkMap.Parse(string.Empty)));
        }
    }
}
