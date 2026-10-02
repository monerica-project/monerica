using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.ProblemReports;

namespace DirectoryManager.Web.Models.ProblemReports
{
    public class ProblemReportQueueViewModel
    {
        public ProblemReportStatus Status { get; set; } = ProblemReportStatus.Pending;

        public IReadOnlyList<ProblemReport> Items { get; set; } = Array.Empty<ProblemReport>();

        public int Total { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;
    }
}
