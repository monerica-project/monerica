using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    // Backing model for the shared _ReviewRepliesBlock partial. The same block is
    // rendered under both crowd reviews and pinned official reviews so that every
    // review type supports the identical "Reply" + threaded-replies experience.
    public class ReviewRepliesBlockViewModel
    {
        public int DirectoryEntryReviewId { get; set; }

        public bool ReviewsDisabled { get; set; }

        public List<DirectoryEntryReviewComment> Replies { get; set; } = new ();

        public IReadOnlyDictionary<string, int> AuthorPostCountsByFingerprint { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
