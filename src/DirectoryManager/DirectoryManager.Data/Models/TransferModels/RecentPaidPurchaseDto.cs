using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class RecentPaidPurchaseDto
    {
        public DateTime PaidDateUtc { get; set; }
        public SponsorshipType SponsorshipType { get; set; }

        // what you quote/store as USD price for the campaign
        public decimal AmountUsd { get; set; }
        public int Days { get; set; }
        public decimal PricePerDayUsd { get; set; }

        // what they actually paid + in what currency (XMR, BTC, etc.)
        public Currency PaidCurrency { get; set; }
        public decimal PaidAmount { get; set; }

        // expiry
        public DateTime ExpiresUtc { get; set; }

        // listing
        public int DirectoryEntryId { get; set; }
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
    }
}
