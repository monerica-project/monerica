namespace DirectoryManager.Web.Models.ListingReports
{
    public class TrustByCountryReportViewModel : ListingReportRangeViewModel
    {
        public List<TrustByCountryRow> Rows { get; set; } = new ();
    }
}
