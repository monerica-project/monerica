using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Validation
{
    public class ReviewOrderProofRulesTests
    {
        [Theory]
        [InlineData("https://pablo.cash/en/swap/", false)]
        [InlineData("https://pablo.cash/", false)]
        [InlineData("https://exchange.example/exchange", false)]
        [InlineData("https://pablo.cash/en/swap/6C0Q-GQI3-JMNG", true)]
        [InlineData("https://www.bitswitch.io/exchange/vwoLvBT5TFB", true)]
        [InlineData("https://sageswap.io/transaction/dfbfd721-6e92-40a0-9be8-3af5e158c81c/guarantee", true)]
        [InlineData("https://moneroswap.net/MSW-6WF3-5KAT#eyJkIjoiODc4d2laYzZaTURq", true)]
        [InlineData("https://exchange.example/order?id=83920174", true)]
        [InlineData("", false)]
        public void Order_link_must_point_at_one_order(string url, bool expected)
        {
            Assert.Equal(expected, ReviewOrderProofRules.LooksLikeOwnOrderLink(url));
        }

        [Theory]
        [InlineData("bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh", true)]
        [InlineData("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", true)]
        [InlineData("0x52908400098527886E0F7030069857D2E4169EE7 sent from my Ledger", true)]
        [InlineData("888tNkZrPN6JsEgekjMnABU4TBzc2Dt29EPAvkRxbANsAnjyPbb3iQ1YBRk1UXcdRsiKc9dhwMVgN5S9cQUiyoogDavup3H", true)]
        [InlineData("https://pablo.cash/en/swap/6C0Q-GQI3-JMNG\nmoderator, please do not share my order URL with public.", false)]
        [InlineData("order id 12345", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Details_must_contain_a_wallet_address(string? text, bool expected)
        {
            Assert.Equal(expected, ReviewOrderProofRules.ContainsWalletAddress(text));
        }
    }
}
