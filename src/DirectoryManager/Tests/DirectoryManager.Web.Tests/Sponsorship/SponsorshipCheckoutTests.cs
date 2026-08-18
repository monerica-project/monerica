using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    public class SponsorshipCheckoutTests
    {
        // ---------------------------------------------------------------------
        // CanPurchaseListing: the checkout lock. A slot is buyable only when the
        // paid sponsors PLUS the in-flight reservations are below the tier's max.
        // ---------------------------------------------------------------------
        [Theory]

        // Category & Subcategory each have exactly one slot.
        [InlineData(SponsorshipType.CategorySponsor, 0, 0, true)]   // open
        [InlineData(SponsorshipType.CategorySponsor, 0, 1, false)]  // someone is checking out
        [InlineData(SponsorshipType.CategorySponsor, 1, 0, false)]  // already sold
        [InlineData(SponsorshipType.SubcategorySponsor, 0, 0, true)]
        [InlineData(SponsorshipType.SubcategorySponsor, 0, 1, false)]
        [InlineData(SponsorshipType.SubcategorySponsor, 1, 0, false)]

        // Main has five slots.
        [InlineData(SponsorshipType.MainSponsor, 0, 0, true)]
        [InlineData(SponsorshipType.MainSponsor, 4, 0, true)]   // one open
        [InlineData(SponsorshipType.MainSponsor, 4, 1, false)]  // 4 paid + 1 held = full
        [InlineData(SponsorshipType.MainSponsor, 3, 1, true)]   // 3 + 1 = 4 < 5
        [InlineData(SponsorshipType.MainSponsor, 3, 2, false)]  // 3 + 2 = 5
        [InlineData(SponsorshipType.MainSponsor, 5, 0, false)]  // sold out
        public void CanPurchaseListing_RespectsPaidPlusReservedAgainstMax(
            SponsorshipType type, int paid, int reserved, bool expected)
        {
            Assert.Equal(expected, SponsoredListingCheckoutHelper.CanPurchaseListing(paid, reserved, type));
        }

        [Theory]
        [InlineData(SponsorshipType.MainSponsor, 5)]
        [InlineData(SponsorshipType.CategorySponsor, 1)]
        [InlineData(SponsorshipType.SubcategorySponsor, 1)]
        public void GetMaxSlotsForType_MatchesConfiguredMaxima(SponsorshipType type, int expected)
        {
            Assert.Equal(expected, SponsoredListingCheckoutHelper.GetMaxSlotsForType(type));
        }

        // ---------------------------------------------------------------------
        // ApplyPercentDiscount: the charged-price math (rounded to cents,
        // away-from-zero). Decimal literals used directly — attribute args can't
        // be decimal, so these are Facts rather than a Theory.
        // ---------------------------------------------------------------------
        [Fact]
        public void ApplyPercentDiscount_ComputesNetPrice()
        {
            Assert.Equal(1657.50m, SponsorshipDiscountHelper.ApplyPercentDiscount(1950m, 15m));
            Assert.Equal(1560.00m, SponsorshipDiscountHelper.ApplyPercentDiscount(1950m, 20m));
            Assert.Equal(1755.00m, SponsorshipDiscountHelper.ApplyPercentDiscount(1950m, 10m));
        }

        [Fact]
        public void ApplyPercentDiscount_ZeroPercentIsUnchanged()
        {
            Assert.Equal(1950m, SponsorshipDiscountHelper.ApplyPercentDiscount(1950m, 0m));
        }

        [Fact]
        public void ApplyPercentDiscount_RoundsToCentsAwayFromZero()
        {
            // 10.10 - (10.10 * 15% = 1.515) = 8.585 → 8.59
            Assert.Equal(8.59m, SponsorshipDiscountHelper.ApplyPercentDiscount(10.10m, 15m));
        }

        // ---------------------------------------------------------------------
        // ResolveCrossTierPerk: the existing-sponsor perk matrix. Cross-tier only
        // (never a same-tier renewal); highest held tier wins (Main 20 > Cat 15 > Sub 10).
        // ---------------------------------------------------------------------
        [Theory]

        // Holds Main → 20% off Category/Subcategory; nothing off Main (renewal).
        [InlineData(SponsorshipType.CategorySponsor, true, false, false, 20)]
        [InlineData(SponsorshipType.SubcategorySponsor, true, false, false, 20)]
        [InlineData(SponsorshipType.MainSponsor, true, false, false, 0)]

        // Holds Category → 15% off Main/Subcategory; nothing off Category.
        [InlineData(SponsorshipType.MainSponsor, false, true, false, 15)]
        [InlineData(SponsorshipType.SubcategorySponsor, false, true, false, 15)]
        [InlineData(SponsorshipType.CategorySponsor, false, true, false, 0)]

        // Holds Subcategory → 10% off Main/Category; nothing off Subcategory.
        [InlineData(SponsorshipType.MainSponsor, false, false, true, 10)]
        [InlineData(SponsorshipType.CategorySponsor, false, false, true, 10)]
        [InlineData(SponsorshipType.SubcategorySponsor, false, false, true, 0)]

        // Holds nothing → no perk.
        [InlineData(SponsorshipType.MainSponsor, false, false, false, 0)]

        // Multiple tiers held → highest wins.
        [InlineData(SponsorshipType.CategorySponsor, true, false, true, 20)]   // Main+Sub, buy Category → 20
        [InlineData(SponsorshipType.MainSponsor, false, true, true, 15)]        // Cat+Sub, buy Main → 15
        public void ResolveCrossTierPerk_FollowsTheMatrix(
            SponsorshipType target, bool holdsMain, bool holdsCategory, bool holdsSubcategory, int expectedPercent)
        {
            var (percent, _) = SponsorshipDiscountHelper.ResolveCrossTierPerk(
                target, holdsMain, holdsCategory, holdsSubcategory);

            Assert.Equal(expectedPercent, percent);
        }
    }
}
