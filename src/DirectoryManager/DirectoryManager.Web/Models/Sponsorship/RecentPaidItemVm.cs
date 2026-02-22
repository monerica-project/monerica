namespace DirectoryManager.Web.Models.Sponsorship
{
    public class RecentPaidItemVm
    {
        public DateTime PaidUtc { get; set; }
        public string SponsorshipType { get; set; } = "";

        public int Days { get; set; }

        public decimal AmountUsd { get; set; }
        public decimal PricePerDayUsd { get; set; }

        // NEW (generic paid info)
        public string PaidCurrency { get; set; } = "";
        public decimal PaidAmount { get; set; }

        // NEW
        public DateTime ExpiresUtc { get; set; }

        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
    }
}
