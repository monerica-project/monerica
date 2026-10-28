using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Activity;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>Admin activity report: everything people submitted to the site, by type and time.</summary>
    public sealed class ActivityReportViewModel
    {
        public string Period { get; set; } = string.Empty;

        public string PeriodLabel { get; set; } = string.Empty;

        public DateTime From { get; set; }

        /// <summary>Exclusive end of the range.</summary>
        public DateTime To { get; set; }

        public IReadOnlyList<(string Key, string Label)> PeriodOptions { get; set; } = Array.Empty<(string, string)>();

        /// <summary>The page's query string, passed on to the chart images.</summary>
        public string ChartQuery { get; set; } = string.Empty;

        public Dictionary<SiteActivityType, int> Totals { get; set; } = new ();

        public int GrandTotal => this.Totals.Values.Sum();

        public List<ActivityBucket> Weeks { get; set; } = new ();

        public List<ActivityBucket> Months { get; set; } = new ();
    }
}
