using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.ProblemReports;

namespace DirectoryManager.Web.Models.ProblemReports
{
    /// <summary>A single problem report with its listing, for the full-text detail page.</summary>
    public class ProblemReportDetailViewModel
    {
        public ProblemReport Request { get; set; } = null!;

        public DirectoryEntry? Entry { get; set; }
    }
}
