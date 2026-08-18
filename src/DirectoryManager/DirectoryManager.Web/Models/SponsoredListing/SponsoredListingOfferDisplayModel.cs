using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.SponsoredListing
{
    public class SponsoredListingOfferDisplayModel
    {
        required public string Description { get; set; }

        public int Days { get; set; }

        public Currency PriceCurrency { get; set; }

        public decimal Price { get; set; }

        public string CategorySubcategory { get; set; } = string.Empty;

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;

        /// <summary>
        /// for CategorySponsor this is the CategoryId,
        /// for SubcategorySponsor this is the SubCategoryId,
        /// for MainSponsor just leave zero.
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// The subcategory this offer row is scoped to (0 for the Default row / Main). Used to
        /// deep-link the row to the public pricing-history explorer for that subcategory.
        /// </summary>
        public int ScopeSubCategoryId { get; set; }

        /// <summary>
        /// true if that slot still has space.
        /// </summary>
        public bool IsAvailable { get; set; }
        public string ActionLink { get; set; }
    }
}
