using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class ActiveSponsoredListingModel
    {
        public int SponsoredListingId { get; set; }
        public DateTime CampaignEndDate { get; set; }
        required public string ListingName { get; set; }
        public int DirectoryListingId { get; set; }
        required public string ListingUrl { get; set; }
        public SponsorshipType SponsorshipType { get; set; }
        public string SubcategoryName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}