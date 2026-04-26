using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    public class LowRatedReviewsViewModel
    {
        public IReadOnlyList<DirectoryEntryReview> Reviews { get; set; } = Array.Empty<DirectoryEntryReview>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int MaxRating { get; set; }
        public ReviewModerationStatus? Status { get; set; }
    }
}