using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class AiCallLogListViewModel
    {
        public IReadOnlyList<AiCallLog> Items { get; set; } = Array.Empty<AiCallLog>();

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int? SubmissionId { get; set; }

        public bool ErrorsOnly { get; set; }

        public IReadOnlyList<AiCallDailyTotal> Daily { get; set; } = Array.Empty<AiCallDailyTotal>();

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(this.Total / (double)Math.Max(1, this.PageSize)));
    }
}
