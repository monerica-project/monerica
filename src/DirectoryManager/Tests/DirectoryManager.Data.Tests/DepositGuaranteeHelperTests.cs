using DirectoryManager.Data.Helpers;

namespace DirectoryManager.Data.Tests
{
    public class DepositGuaranteeHelperTests
    {
        [Theory]
        [InlineData("https://orangefren.com/some/path", "orangefren.com")]
        [InlineData("https://bitcointalk.org/index.php?topic=123", "bitcointalk.org")]
        [InlineData("http://www.example.com/x", "example.com")]      // www. stripped
        [InlineData("orangefren.com/no-scheme", "orangefren.com")]   // scheme inferred
        [InlineData("https://sub.domain.co.uk/", "sub.domain.co.uk")] // subdomain kept
        public void ProofLinkLabel_ReturnsBareHostname(string url, string expected)
        {
            Assert.Equal(expected, DepositGuaranteeHelper.ProofLinkLabel(url));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a url")]
        public void ProofLinkLabel_FallsBackWhenUnparseable(string? url)
        {
            Assert.Equal("view proof", DepositGuaranteeHelper.ProofLinkLabel(url));
        }

        private static DirectoryManager.Data.Models.GuaranteeItem G(decimal amount, DirectoryManager.Data.Enums.Currency cur) =>
            new () { Amount = amount, Currency = cur, Link = "https://example.com" };

        [Fact]
        public void SummaryLine_MixedCurrencies_ListsEachWithPlus_NoTotal()
        {
            var line = DepositGuaranteeHelper.SummaryLine(new[]
            {
                G(5000m, DirectoryManager.Data.Enums.Currency.USD),
                G(0.40955328m, DirectoryManager.Data.Enums.Currency.BTC),
            });
            Assert.StartsWith("2 deposits: ", line);
            Assert.Contains(" + ", line);
            Assert.DoesNotContain("totaling", line);
        }

        [Fact]
        public void SummaryLine_OneCurrency_KeepsTheTotal()
        {
            var line = DepositGuaranteeHelper.SummaryLine(new[]
            {
                G(5000m, DirectoryManager.Data.Enums.Currency.USD),
                G(10000m, DirectoryManager.Data.Enums.Currency.USD),
            });
            Assert.StartsWith("2 deposits, totaling ", line);
            Assert.DoesNotContain("+", line);
        }
    }
}
