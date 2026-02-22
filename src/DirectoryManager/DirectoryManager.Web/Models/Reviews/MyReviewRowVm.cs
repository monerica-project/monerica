using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{

    public class MyReviewRowVm
    {
        public DirectoryEntryReview Review { get; set; } = default!;

        // All replies for this review thread (you can choose approved-only or all)
        public List<DirectoryEntryReviewComment> Comments { get; set; } = new ();
    }
}
