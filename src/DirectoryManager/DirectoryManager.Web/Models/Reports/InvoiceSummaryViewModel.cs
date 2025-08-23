namespace DirectoryManager.Web.Models.Reports
{
    public class InvoiceSummaryViewModel
    {
        public int SponsoredListingInvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public DateTime CampaignStartDate { get; set; }
        public DateTime CampaignEndDate { get; set; }
        public double AvgUsdPerDay { get; set; }
    }
}
