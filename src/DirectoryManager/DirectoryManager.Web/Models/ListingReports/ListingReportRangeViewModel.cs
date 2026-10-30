namespace DirectoryManager.Web.Models.ListingReports
{
    /// <summary>Date range shared by every listing report page (dates are UTC, end inclusive).</summary>
    public class ListingReportRangeViewModel
    {
        /// <summary>Which report page this is: "active", "categories", "countries" or "trust".</summary>
        public string Section { get; set; } = string.Empty;

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        /// <summary>Oldest date the edit history covers.</summary>
        public DateTime Earliest { get; set; }

        /// <summary>True for pages that show a single "as of" date instead of a range.</summary>
        public bool SingleDate { get; set; }
    }
}
