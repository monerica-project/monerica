using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    /// <summary>The admin reply page: the reply itself, and whether its body is being edited.</summary>
    public sealed class ShowReplyViewModel
    {
        public DirectoryEntryReviewComment Comment { get; init; } = null!;

        public bool IsEditMode { get; init; }
    }
}
