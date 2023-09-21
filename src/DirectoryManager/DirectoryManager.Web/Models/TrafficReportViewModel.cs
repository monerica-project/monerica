namespace DirectoryManager.Web.Models
{
    public class TrafficReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int UniqueIPCount { get; set; }
        public int TotalLogCount { get; set; }
    }
}
