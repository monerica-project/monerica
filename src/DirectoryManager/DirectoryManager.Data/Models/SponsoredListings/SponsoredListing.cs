using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListing : StateInfo
    {
        public int SponsoredListingId { get; set; }

        public DateTime CampaignStartDate { get; set; }

        public DateTime CampaignEndDate { get; set; }

        public int DirectoryEntryId { get; set; }

        /// <summary>
        /// The most recently paid invoice for this sponsored listing.
        /// </summary>
        public int SponsoredListingInvoiceId { get; set; }

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;

        public int? SubCategoryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public virtual SponsoredListingInvoice? SponsoredListingInvoice { get; set; }
    }
}