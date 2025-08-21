namespace DirectoryManager.Web.Models
{
    public class EntryReviewsBlockViewModel
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;

        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public IReadOnlyList<EntryReviewItem> Reviews { get; set; } = Array.Empty<EntryReviewItem>();
    }
}
