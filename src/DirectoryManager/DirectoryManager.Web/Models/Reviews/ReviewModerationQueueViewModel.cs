using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    public class ReviewModerationQueueViewModel
    {
        public IReadOnlyList<DirectoryEntryReview> PendingReviews { get; set; } = new List<DirectoryEntryReview>();
        public IReadOnlyList<DirectoryEntryReviewComment> PendingReplies { get; set; } = new List<DirectoryEntryReviewComment>();
    }
}
