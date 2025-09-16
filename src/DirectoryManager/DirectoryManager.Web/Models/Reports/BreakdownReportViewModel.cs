using DirectoryManager.Web.Models.Reports;

namespace DirectoryManager.Web.Models.Reports
{
    public class BreakdownReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<BreakdownRow> MainSponsorBreakdown { get; set; } = [];
        public List<BreakdownRow> SubcategoryBreakdown { get; set; } = [];
        public List<BreakdownRow> CategoryBreakdown { get; set; } = [];
    }
}