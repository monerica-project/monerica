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
        /// for MainSponsor just leave zero
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// true if that slot still has space
        /// </summary>
        public bool IsAvailable { get; set; }
        public string ActionLink { get; set; }
    }
}
