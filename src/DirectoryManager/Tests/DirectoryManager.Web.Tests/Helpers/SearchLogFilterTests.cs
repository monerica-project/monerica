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
        [InlineData("test")]           // 4,401 in one day (2026-05-02)
        [InlineData("Test' AND 1")]
        [InlineData("'\"()")]
        [InlineData("../e")]
        [InlineData("/etc/shells")]
        [InlineData("vpn'||'1")]
        [InlineData("vps AND 1=1")]
        [InlineData("!(()&&!|*|*|")]
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
        [InlineData("https://veil.la/")]
        [InlineData("can I ask you a question please?")]
        [InlineData("land or sea")]
        [InlineData("testnet faucet for monero stagenet with a long descriptive query")]
        public void RealSearches_AreLogged(string term)
        {
            Assert.True(SearchLogFilter.ShouldLog(term));
        }

        [Fact]
        public void RepeatGuard_LogsATermOnlyAFewTimesPerHour()
        {
            SearchLogRepeatGuard.Reset();
            var t0 = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
            var allowed = Enumerable.Range(0, 200).Count(i => SearchLogRepeatGuard.Allow("Sim", t0.AddSeconds(i)));
            Assert.Equal(SearchLogRepeatGuard.MaxPerTermPerHour, allowed);
            Assert.False(SearchLogRepeatGuard.Allow("sim", t0.AddMinutes(30)));   // same term, other casing
            Assert.True(SearchLogRepeatGuard.Allow("vps", t0.AddMinutes(30)));    // other terms unaffected
            Assert.True(SearchLogRepeatGuard.Allow("sim", t0.AddHours(1).AddSeconds(1))); // next hour
        }
    }
}
