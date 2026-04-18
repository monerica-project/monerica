using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class RecentPaidItemVm
    {
        public DateTime PaidUtc { get; set; }

        // existing label you already show in the table
        public string SponsorshipType { get; set; } = string.Empty;

        // ✅ NEW: real enum so we can switch reliably
        public SponsorshipType SponsorshipTypeEnum { get; set; }

        // ✅ NEW: used to resolve category/subcategory keys (single lookup)
        public int? DirectoryEntryId { get; set; }

        // ✅ NEW: where the "Type" label should link to
        public string PlacementUrl { get; set; } = string.Empty;

        public int Days { get; set; }

        public string PaidCurrency { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }

        public decimal AmountUsd { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public decimal PricePerDayUsd { get; set; }

        public string ListingName { get; set; } = string.Empty;
        public string ListingUrl { get; set; } = string.Empty;
    }
}