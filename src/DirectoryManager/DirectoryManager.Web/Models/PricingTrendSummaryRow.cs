using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>One placement-type row in the pricing-trends summary table.</summary>
    public class PricingTrendSummaryRow
    {
        public SponsorshipType Type { get; set; }

        public string TypeLabel { get; set; } = string.Empty;

        public decimal AvgPerDay { get; set; }

        public decimal MinPerDay { get; set; }

        public decimal MaxPerDay { get; set; }

        public int DaysWithData { get; set; }

        public int PaidInvoices { get; set; }

        public long ActiveListingDays { get; set; }
    }
}
