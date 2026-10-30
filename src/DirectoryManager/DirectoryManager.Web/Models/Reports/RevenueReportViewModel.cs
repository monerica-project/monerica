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

        /// <summary>True when every amount on the page (and its charts) is in XMR.</summary>
        public bool InXmr { get; init; }

        /// <summary>The period's grand total in the other unit (XMR when showing USD, and vice versa).</summary>
        public decimal OtherUnitTotal { get; init; }

        /// <summary>Payments in the period with no XMR amount recorded (left out of the XMR view).</summary>
        public int NotPaidInXmrCount { get; init; }

        /// <summary>USD total of those payments.</summary>
        public decimal NotPaidInXmrUsd { get; init; }

        /// <summary>The months in the by-month chart, oldest first (same data as the bars).</summary>
        public List<RevenueMonth> ChartMonths { get; init; } = new ();
    }

}
