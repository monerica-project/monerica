namespace DirectoryManager.Web.Models.ListingReports
{
    public class ListingBreakdownReportViewModel : ListingReportRangeViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string ColumnLabel { get; set; } = string.Empty;

        public List<ListingReportBreakdownRow> Rows { get; set; } = new ();

        /// <summary>Active listings with no value for this breakdown (e.g. no country set) at the end date.</summary>
        public int MissingAtEnd { get; set; }
    }
}
