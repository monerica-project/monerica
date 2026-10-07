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
    }
}
