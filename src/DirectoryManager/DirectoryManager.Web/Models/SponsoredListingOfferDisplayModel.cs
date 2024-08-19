using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SponsoredListingOfferDisplayModel
    {
        required public string Description { get; set; }

        public int Days { get; set; }

        public Currency PriceCurrency { get; set; }

        public decimal Price { get; set; }

        public string CategorySubcategory { get; set; } = string.Empty;

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;
    }
}
