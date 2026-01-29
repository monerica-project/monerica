using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class ReviewModerationDashboardViewModel
    {
        public ReviewModerationStatus? Status { get; set; }

        public IReadOnlyList<DirectoryEntryReview> Reviews { get; set; } = Array.Empty<DirectoryEntryReview>();
        public int ReviewsTotal { get; set; }
        public int ReviewsPage { get; set; } = 1;
        public int ReviewsPageSize { get; set; } = 50;

        public IReadOnlyList<DirectoryEntryReviewComment> Replies { get; set; } = Array.Empty<DirectoryEntryReviewComment>();
        public int RepliesTotal { get; set; }
        public int RepliesPage { get; set; } = 1;
        public int RepliesPageSize { get; set; } = 50;
    }
}
