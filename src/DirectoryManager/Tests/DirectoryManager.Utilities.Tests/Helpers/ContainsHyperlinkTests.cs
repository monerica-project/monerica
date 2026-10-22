using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    public class ContainsHyperlinkTests
    {
        [Theory]
        [InlineData("check scam-shop.io/x for the order")]
        [InlineData("their mirror foo.i2p works")]
        [InlineData("go to bar.xyz")]
        [InlineData("Visit Example.COM")]
        [InlineData("https://a.b/c")]
        [InlineData("sub.domain.co.uk is down")]
        public void DetectsLinks(string text) => Assert.True(StringHelpers.ContainsHyperlink(text));

        [Theory]
        [InlineData("Fast swap, good rates. 10/10 would use again.")]
        [InlineData("e.g. it took 5 minutes, i.e. quick")]
        [InlineData("Version v0.18.3 works fine, sent 1.5 XMR")]
        [InlineData("email me at someone@example")]
        [InlineData("")]
        public void IgnoresOrdinaryText(string text) => Assert.False(StringHelpers.ContainsHyperlink(text));
    }
}
