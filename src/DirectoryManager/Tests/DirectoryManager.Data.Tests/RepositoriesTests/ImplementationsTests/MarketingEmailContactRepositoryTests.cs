using DirectoryManager.Data.Repositories.Implementations;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class MarketingEmailContactRepositoryTests
    {
        [Theory]
        [InlineData("  Support@Example.COM ", "support@example.com")]
        [InlineData(null, "")]
        [InlineData("   ", "")]
        public void NormalizeEmail_TrimsAndLowercases(string? input, string expected)
        {
            Assert.Equal(expected, MarketingEmailContactRepository.NormalizeEmail(input));
        }

        [Fact]
        public void NewToken_Is32LowercaseHexAndDifferentEachTime()
        {
            string a = MarketingEmailContactRepository.NewToken();
            string b = MarketingEmailContactRepository.NewToken();

            Assert.Matches("^[a-f0-9]{32}$", a);
            Assert.Matches("^[a-f0-9]{32}$", b);
            Assert.NotEqual(a, b);
        }
    }
}
