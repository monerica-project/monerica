using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    // The public pricing-history picker: one value per placement, parsed back exactly.
    public class PricingPlacementTests
    {
        [Theory]
        [InlineData(null, null, "")]
        [InlineData(SponsorshipType.MainSponsor, null, "main")]
        [InlineData(SponsorshipType.CategorySponsor, null, "category")]
        [InlineData(SponsorshipType.CategorySponsor, 8, "category-8")]
        [InlineData(SponsorshipType.SubcategorySponsor, null, "subcategory")]
        [InlineData(SponsorshipType.SubcategorySponsor, 73, "subcategory-73")]
        public void Value_RoundTrips(SponsorshipType? type, int? id, string expected)
        {
            var value = SponsorshipController.PricingPlacementValue(type, id);
            Assert.Equal(expected, value);

            var (parsedType, parsedId) = SponsorshipController.ParsePricingPlacement(value);
            Assert.Equal(type, parsedType);
            Assert.Equal(id, parsedId);
        }

        [Theory]
        [InlineData("category-0")]
        [InlineData("category-abc")]
        [InlineData("subcategory--5")]
        [InlineData("nonsense")]
        [InlineData("<script>")]
        public void Parse_BadInput_MeansAllPlacements(string input)
        {
            var (type, id) = SponsorshipController.ParsePricingPlacement(input);
            Assert.Null(type);
            Assert.Null(id);
        }
    }
}
