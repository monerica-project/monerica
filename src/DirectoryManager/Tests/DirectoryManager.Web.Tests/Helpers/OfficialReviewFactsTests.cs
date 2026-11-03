using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class OfficialReviewFactsTests
    {
        private const string SageSwapReview =
            "Exchange: 0.0203458 BTC to 2.8786466 XMR\r\n\r\n" +
            "Rate Type: Float (default for mode)\r\n\r\n" +
            "Mode: No AML\r\n\r\n" +
            "Required Confirmations: 1 for BTC\r\n\r\n" +
            "Processing Time: ~2 minutes\r\n\r\n" +
            "AML Bot (on sent BTC): High Risk (High Risk mixer | Medium risk unnamed service)\r\n\r\n" +
            "Fee: ~2.5%\r\n\r\n" +
            "KYC: No\r\n\r\n" +
            "Connection: VPN\r\n\r\n" +
            "Comments: Had received high AML coins. SageSwap processed these coins without an issue.";

        [Fact]
        public void Parse_SplitsLabelLinesIntoFactsAndComment()
        {
            var f = OfficialReviewFacts.Parse(SageSwapReview);

            Assert.True(f.IsStructured);
            Assert.Equal(9, f.Facts.Count);
            Assert.Equal("Exchange", f.Facts[0].Key);
            Assert.Equal("0.0203458 BTC to 2.8786466 XMR", f.Facts[0].Value);
            Assert.Equal("AML Bot (on sent BTC)", f.Facts[5].Key);
            Assert.Equal("Connection", f.Facts[8].Key);
            Assert.StartsWith("Had received high AML coins.", f.Comments);
        }

        [Fact]
        public void Parse_MultiLineCommentIsKeptWhole()
        {
            var f = OfficialReviewFacts.Parse("Exchange: a\nFee: 1%\nKYC: No\nComments: first line\nsecond line");
            Assert.True(f.IsStructured);
            Assert.Equal("first line\nsecond line", f.Comments);
        }

        [Fact]
        public void Parse_WrappedFactLineJoinsTheFactNotTheNotes()
        {
            var f = OfficialReviewFacts.Parse(
                "Exchange: 3 XMR to 0.019 BTC\n" +
                "AML Bot (on received BTC): Medium risk, Direct Interactions with platforms:\n" +
                "hitbtc-unionchain.ai, CCE.cash\n" +
                "Fee: normal\n" +
                "KYC: No\n" +
                "Comments: Overall good.");

            Assert.True(f.IsStructured);
            Assert.Equal("Medium risk, Direct Interactions with platforms: hitbtc-unionchain.ai, CCE.cash", f.Facts[1].Value);
            Assert.Equal("Overall good.", f.Comments);
        }

        [Theory]
        [InlineData("Great service, swapped in two minutes. Would use again.")]
        [InlineData("Fee: 1%\nThe rest is a normal paragraph.\nAnother sentence here.")]
        [InlineData("")]
        [InlineData(null)]
        public void Parse_FreeTextIsNotStructured(string? body) =>
            Assert.False(OfficialReviewFacts.Parse(body).IsStructured);
    }
}
