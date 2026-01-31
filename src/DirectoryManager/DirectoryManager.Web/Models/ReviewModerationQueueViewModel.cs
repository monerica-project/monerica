using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class ReviewModerationQueueViewModel
    {
        public IReadOnlyList<DirectoryEntryReview> PendingReviews { get; set; } = new List<DirectoryEntryReview>();
        public IReadOnlyList<DirectoryEntryReviewComment> PendingReplies { get; set; } = new List<DirectoryEntryReviewComment>();
    }
}
