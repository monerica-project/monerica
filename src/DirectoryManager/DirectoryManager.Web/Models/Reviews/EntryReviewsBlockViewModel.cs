using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
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

        // ✅ paging
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // ✅ needed to build /site/{key}/page/{n}
        public string DirectoryEntryKey { get; set; } = string.Empty;

        public int Rating5Count { get; set; }
        public int Rating4Count { get; set; }
        public int Rating3Count { get; set; }
        public int Rating2Count { get; set; }
        public int Rating1Count { get; set; }

        public int RatingTotalCount => this.Rating1Count + this.Rating2Count + this.Rating3Count + this.Rating4Count + this.Rating5Count;
    }
}
