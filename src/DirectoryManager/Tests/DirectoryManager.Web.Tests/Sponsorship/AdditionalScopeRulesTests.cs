using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    // A listing can be in a category only through an additional subcategory (an exchange that also
    // runs a block explorer listed under Utilities). It may sponsor that category too.
    public class AdditionalScopeRulesTests
    {
        [Fact]
        public void Subcategory_AlwaysKeepsTheAdditionalSubcategory() =>
            Assert.True(SponsoredListingCheckoutHelper.AdditionalScopeApplies(SponsorshipType.SubcategorySponsor, 8, 8));

        [Fact]
        public void Category_KeepsItOnlyWhenItIsInAnotherCategory()
        {
            Assert.True(SponsoredListingCheckoutHelper.AdditionalScopeApplies(SponsorshipType.CategorySponsor, 8, 17));
            Assert.False(SponsoredListingCheckoutHelper.AdditionalScopeApplies(SponsorshipType.CategorySponsor, 8, 8));
            Assert.False(SponsoredListingCheckoutHelper.AdditionalScopeApplies(SponsorshipType.CategorySponsor, 8, null));
        }

        [Fact]
        public void MainSponsor_IsNeverScoped() =>
            Assert.False(SponsoredListingCheckoutHelper.AdditionalScopeApplies(SponsorshipType.MainSponsor, 8, 17));
    }
}
