using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    // Admin-blocked websites (e.g. file hosts) can't be used as submission links.
    public class BlockedDomainTests
    {
        private static readonly string[] Blocked = { "muse.ai" };

        [Theory]
        [InlineData("https://muse.ai/")]
        [InlineData("https://www.muse.ai/files/abc/index.html")]
        [InlineData("http://MUSE.AI/x")]
        [InlineData("muse.ai/files/x")]
        [InlineData("https://files.muse.ai/v/1")]
        public void LinksOnBlockedDomainOrSubdomain_AreBlocked(string url)
        {
            Assert.Equal("muse.ai", UrlHelper.MatchBlockedDomain(url, Blocked));
        }

        [Theory]
        [InlineData("https://notmuse.ai/")]
        [InlineData("https://muse.ai.example.com/")]
        [InlineData("https://example.com/?ref=muse.ai")]
        [InlineData("")]
        [InlineData(null)]
        public void OtherLinks_AreNotBlocked(string? url)
        {
            Assert.Null(UrlHelper.MatchBlockedDomain(url, Blocked));
        }

        [Theory]
        [InlineData("muse.ai", "muse.ai")]
        [InlineData("https://www.Muse.ai/files/x", "muse.ai")]
        [InlineData("*.muse.ai", "muse.ai")]
        [InlineData(" files.muse.ai. ", "files.muse.ai")]
        [InlineData("localhost", null)]
        [InlineData("   ", null)]
        public void NormalizeDomain_StoresBareLowerCaseHost(string input, string? expected)
        {
            Assert.Equal(expected, UrlHelper.NormalizeDomain(input));
        }
    }
}
