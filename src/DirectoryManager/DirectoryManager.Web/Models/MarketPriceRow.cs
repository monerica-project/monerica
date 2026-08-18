using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>One placement-type row in the "Suggested Market Price" table.</summary>
    public class MarketPriceRow
    {
        public SponsorshipType Type { get; set; }

        public string TypeLabel { get; set; } = string.Empty;

        public bool HasData { get; set; }

        public decimal SuggestedPerDay { get; set; }

        public decimal SuggestedPerCampaign { get; set; }

        public decimal MedianPerDay { get; set; }

        public decimal P25PerDay { get; set; }

        public decimal P75PerDay { get; set; }

        public bool HasRecent { get; set; }

        public decimal RecentPerDay { get; set; }

        public double? TrendPercent { get; set; }

        public int TypicalCampaignDays { get; set; }

        public int PaidInvoices { get; set; }

        public long ActiveListingDays { get; set; }
    }
}
