using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models
{
    public class ReviewModerationReviewViewModel
    {
        public DirectoryEntryReview Review { get; set; } = null!;

        // Single input field shown in the UI.
        // On GET: populated from Review.OrderUrl ?? Review.OrderId
        public string? OrderProof { get; set; }

        public List<int> SelectedTagIds { get; set; } = new ();

        public List<TagOption> AllTags { get; set; } = new ();

        public ReviewModerationStatus ModerationStatus => this.Review.ModerationStatus;

        public class TagOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsEnabled { get; set; }
        }
    }
}
