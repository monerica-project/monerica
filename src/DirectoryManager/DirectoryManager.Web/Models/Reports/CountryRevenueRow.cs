namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>
    /// One row in the "income by country code" breakdown on the revenue report.
    /// Totals are expressed in the report's selected display currency.
    /// </summary>
    public class CountryRevenueRow
    {
        /// <summary>
        /// ISO country code from the advertiser's DirectoryEntry, upper-cased.
        /// "Unknown" when the entry has no country code set.
        /// </summary>
        public string CountryCode { get; set; } = "Unknown";

        /// <summary>
        /// Best-effort friendly name (e.g. "United States") resolved from the code,
        /// falling back to the raw code when it cannot be resolved.
        /// </summary>
        public string CountryName { get; set; } = "Unknown";

        /// <summary>
        /// Total paid revenue attributed to this country, in the display currency.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Number of paid invoices attributed to this country in the window.
        /// </summary>
        public int InvoiceCount { get; set; }

        /// <summary>
        /// Share of the windowed total, 0-100.
        /// </summary>
        public double Percent { get; set; }
    }
}
