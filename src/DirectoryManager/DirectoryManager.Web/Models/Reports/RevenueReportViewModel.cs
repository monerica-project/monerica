using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Models.Reports
{
    public class RevenueReportViewModel
    {
        public string Period { get; init; } = "this-month";

        public string PeriodLabel { get; init; } = string.Empty;

        public DateTime From { get; init; }

        // Exclusive.
        public DateTime To { get; init; }

        public RevenueBasis Basis { get; init; }

        public RevenueReport Report { get; init; } = new ();

        public IReadOnlyList<(string Key, string Label)> PeriodOptions { get; init; } = Array.Empty<(string, string)>();

        // The request's query string, reused for the chart image URLs.
        public string ChartQuery { get; init; } = string.Empty;

        public int MissingUsdCount { get; init; }
    }

}
