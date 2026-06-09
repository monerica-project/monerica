using System;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>
    /// One projected month on the income forecast. Amounts are in the report's display currency.
    /// </summary>
    public class ForecastMonthRow
    {
        public DateTime Month { get; set; }

        public decimal Expected { get; set; }
        public decimal Low { get; set; }
        public decimal High { get; set; }

        public decimal CumulativeExpected { get; set; }
        public decimal CumulativeLow { get; set; }
        public decimal CumulativeHigh { get; set; }
    }
}
