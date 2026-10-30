using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.ListingReports
{
    public class ListingReportHomeViewModel : ListingReportRangeViewModel
    {
        public int ActiveNow { get; set; }

        public Dictionary<DirectoryStatus, int> ActiveByStatus { get; set; } = new ();

        public int Added30Days { get; set; }

        public int Removed30Days { get; set; }

        public int ActiveYearAgo { get; set; }
    }
}
