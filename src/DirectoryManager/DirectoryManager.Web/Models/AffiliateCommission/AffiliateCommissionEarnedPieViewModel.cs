using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models.AffiliateCommissionPaid
{
    public class AffiliateCommissionEarnedPieViewModel
    {
        public string Period { get; set; } = "all";
        public string PeriodLabel { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; } // exclusive
        public IReadOnlyList<(string Key, string Label)> PeriodOptions { get; set; } = Array.Empty<(string, string)>();
        public List<AffiliateCommissionEarnedTotal> Totals { get; set; } = new ();
        public decimal GrandTotalUsd { get; set; }
        public string ChartQuery { get; set; } = string.Empty;
    }
}
