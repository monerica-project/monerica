using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class EntryReviewsBlockViewModel
    {
        public int DirectoryEntryId { get; set; }

        // Used by _EntryReviews.cshtml
        public int ReviewCount { get; set; }
        public double? AverageRating { get; set; }

        public List<DirectoryEntryReview> Reviews { get; set; } = new ();

        // key = DirectoryEntryReviewId
        public Dictionary<int, List<DirectoryEntryReviewComment>> RepliesByReviewId { get; set; }
            = new ();
    }
}
