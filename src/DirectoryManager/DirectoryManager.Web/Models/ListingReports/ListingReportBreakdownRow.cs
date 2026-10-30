namespace DirectoryManager.Web.Models.ListingReports
{
    /// <summary>One row of a start-vs-end comparison (a category or a country).</summary>
    public class ListingReportBreakdownRow
    {
        public string Label { get; set; } = string.Empty;

        public int StartCount { get; set; }

        public int EndCount { get; set; }

        public int Change => this.EndCount - this.StartCount;
    }
}
