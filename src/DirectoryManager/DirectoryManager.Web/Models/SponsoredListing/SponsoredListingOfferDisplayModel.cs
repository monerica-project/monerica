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
        /// Value for the public pricing-history picker ("main", "category-8", "subcategory",
        /// "subcategory-73"), so each row links to the history for exactly that placement.
        /// </summary>
        public string HistoryPlacement { get; set; } = string.Empty;

        /// <summary>True when this placement has no price of its own and uses the standard price.</summary>
        public bool IsStandardPrice { get; set; }

        /// <summary>
        /// true if that slot still has space.
        /// </summary>
        public bool IsAvailable { get; set; }
        public string ActionLink { get; set; }
    }
}
