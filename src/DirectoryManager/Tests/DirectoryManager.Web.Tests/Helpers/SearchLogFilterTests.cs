using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SearchLogFilterTests
    {
        [Theory]
        [InlineData("test'")]          // 204 in one scanner burst (2026-09-27)
        [InlineData("test\"")]         // 209 in the same burst
        [InlineData("1'")]
        [InlineData("{searchTerms}")]   // unfilled OpenSearch template, 36
        [InlineData("{search_term_string}")]
        [InlineData("'")]
        [InlineData(",,',.)(),")]
        [InlineData("x")]
        [InlineData("2c31faf18fd76511f11c139cd05bf8933b2d12ac")]
        [InlineData("   ")]
        public void ProbeShapedTerms_AreNotLogged(string term)
        {
            Assert.False(SearchLogFilter.ShouldLog(term));
        }

        [Theory]
        [InlineData("vps")]
        [InlineData("ai")]
        [InlineData("ward's")]
        [InlineData("xmr.cards")]
        [InlineData("gift card")]
        [InlineData("2026")]
        [InlineData("域名")]
        [InlineData("no kyc")]
        public void RealSearches_AreLogged(string term)
        {
            Assert.True(SearchLogFilter.ShouldLog(term));
        }
    }
}
