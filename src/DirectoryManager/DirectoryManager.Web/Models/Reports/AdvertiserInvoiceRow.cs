namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserInvoiceRow
    {
        public int SponsoredListingInvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";

        public DateTime CampaignStartDate { get; set; }
        public DateTime CampaignEndDate { get; set; }

        public double AvgUsdPerDay { get; set; }
        public string SponsorshipType { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreateDate { get; set; }

        public int DaysPurchased { get; set; }

    }
}
