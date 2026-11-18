using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Models.AffiliateCommissionPaid
{
    public class AffiliateCommissionEarnedByMonthViewModel
    {
        public AffiliateCommissionMonthlyReport.Result Report { get; set; } = null!;

        public IReadOnlyList<(string Key, string Label)> RangeOptions { get; set; } = AffiliateCommissionMonthlyReport.RangeOptions;

        /// <summary>Months offered in the custom start/end selects, newest first.</summary>
        public List<DateTime> MonthOptions { get; set; } = new ();

        public int? CompanyId { get; set; }

        public string? CompanyName { get; set; }

        public List<string> Notices { get; set; } = new ();

        /// <summary>Canonical query (range, months, company) shared by the chart image and the links.</summary>
        public string RangeQuery { get; set; } = string.Empty;

        public string ChartQuery { get; set; } = string.Empty;
    }
}
