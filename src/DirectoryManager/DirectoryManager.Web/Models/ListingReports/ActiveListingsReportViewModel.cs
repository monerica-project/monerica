namespace DirectoryManager.Web.Models.ListingReports
{
    public class ActiveListingsReportViewModel : ListingReportRangeViewModel
    {
        public int ActiveAtStart { get; set; }

        public int ActiveAtEnd { get; set; }

        public List<ListingReportEntryRow> Added { get; set; } = new ();

        public List<ListingReportEntryRow> Removed { get; set; } = new ();
    }
}
