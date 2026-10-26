using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Tests.Seo
{
    public class LlmsAndRobotsTests
    {
        [Fact]
        public void ListingFacts_IncludesKnownFactsOnly()
        {
            var entry = new DirectoryEntry
            {
                Name = "X",
                DirectoryEntryKey = "x",
                KycPolicy = KycPolicy.GuaranteedNoKyc,
                Liquidity = Liquidity.Own,
                Link2 = "http://abc.onion",
            };

            var facts = LlmsController.ListingFacts(entry, "Tor", "I2P", 4.66, 12);
            Assert.Equal("KYC: Guaranteed No KYC; Liquidity: Own; Tor: yes; I2P: no; Rating: 4.7/5 (12 reviews)", facts);
        }

        [Fact]
        public void ListingFacts_HidesUnknownKycAndNotApplicableLiquidity()
        {
            var entry = new DirectoryEntry { Name = "X", DirectoryEntryKey = "x", Liquidity = Liquidity.NotApplicable };
            var facts = LlmsController.ListingFacts(entry, null, null, null, 0);
            Assert.Equal("Rating: no reviews yet", facts);
        }

        [Fact]
        public void Robots_AllowsTheMainAiCrawlers()
        {
            foreach (var bot in new[] { "GPTBot", "ClaudeBot", "PerplexityBot", "Google-Extended", "CCBot" })
            {
                Assert.Contains(bot, RobotsController.AllowedCrawlers);
            }
        }
    }
}
