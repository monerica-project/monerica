using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Seo
{
    public class SeoDescriptionBuilderTests
    {
        private static ListingDescriptionFacts Trocador() => new ()
        {
            Name = "Trocador",
            SubcategoryName = "Aggregators",
            CurrencyName = "Monero (XMR)",
            Status = DirectoryStatus.Verified,
            KycPolicy = KycPolicy.GuaranteedNoKyc,
            Liquidity = Liquidity.NotApplicable,
            AverageRating = 4.66,
            ReviewCount = 31,
            HasLink2 = true,
            Link2Name = "Tor",
            Link3Name = "I2P",
            Description = "A privacy-focused exchange aggregator with minimal JavaScript.",
        };

        [Fact]
        public void Listing_LeadsWithFactsThenDescription()
        {
            var d = SeoDescriptionBuilder.ForListing(Trocador());
            Assert.StartsWith("Trocador (Monero Aggregators): Verified. Guaranteed no KYC. Rated 4.7/5 from 31 reviews. Tor site available.", d);
            Assert.Contains("privacy-focused", d);
            Assert.True(d.Length <= SeoDescriptionBuilder.MaxLength, d.Length.ToString());
            Assert.DoesNotContain("liquidity", d); // Not Applicable is not shown
        }

        [Fact]
        public void Listing_LongDescriptionIsCutWithEllipsis()
        {
            var facts = Trocador();
            facts.Description = string.Join(" ", Enumerable.Repeat("word", 80));
            var d = SeoDescriptionBuilder.ForListing(facts);
            Assert.True(d.Length <= SeoDescriptionBuilder.MaxLength);
            Assert.EndsWith("…", d);
        }

        [Fact]
        public void Listing_ShowsLiquidityTorAndI2P()
        {
            var facts = Trocador();
            facts.Liquidity = Liquidity.Own;
            facts.HasLink3 = true;
            var d = SeoDescriptionBuilder.ForListing(facts);
            Assert.Contains("Own liquidity", d);
            Assert.Contains("Tor and I2P sites available", d);
        }

        [Fact]
        public void Listing_ScamIsFlaggedAndNoReviewsMeansNoRating()
        {
            var facts = Trocador();
            facts.Status = DirectoryStatus.Scam;
            facts.ReviewCount = 0;
            facts.AverageRating = null;
            var d = SeoDescriptionBuilder.ForListing(facts);
            Assert.Contains("Flagged as a scam", d);
            Assert.DoesNotContain("Rated", d);
        }

        [Fact]
        public void Listing_SingleReviewIsSingular()
        {
            var facts = Trocador();
            facts.ReviewCount = 1;
            facts.AverageRating = 5;
            Assert.Contains("from 1 review.", SeoDescriptionBuilder.ForListing(facts));
        }

        [Fact]
        public void Listing_DifferentListingsGetDifferentDescriptions()
        {
            var a = Trocador();
            var b = Trocador();
            b.Name = "Other Swap";
            Assert.NotEqual(SeoDescriptionBuilder.ForListing(a), SeoDescriptionBuilder.ForListing(b));
        }

        [Fact]
        public void ListPage_FallbackFits()
        {
            var d = SeoDescriptionBuilder.ForListPage("Centralized Exchanges (Exchanges)", 12, "Monero (XMR)", "Monerica");
            Assert.StartsWith("Browse 12 Centralized Exchanges (Exchanges) listings that accept or relate to Monero (XMR) on Monerica.", d);
            Assert.True(d.Length <= SeoDescriptionBuilder.MaxLength);
        }

        [Fact]
        public void Shorten_LeavesShortTextAlone()
        {
            Assert.Equal("Hello world.", SeoDescriptionBuilder.Shorten("  Hello   world. "));
        }
    }
}
