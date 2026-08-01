namespace DirectoryManager.Web.Models.Reviews
{
    /// <summary>Backs the upload / delete-confirmation pages for a review image field.</summary>
    public class ReviewImageActionViewModel
    {
        public int DirectoryEntryReviewId { get; set; }

        // "image" (Screenshot / ImageUrl) or "aml" (AmlScreenshotUrl).
        public string Field { get; set; } = "image";

        public string FieldLabel { get; set; } = "Screenshot";

        public string? CurrentUrl { get; set; }
    }
}
