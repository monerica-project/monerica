using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models.Submissions
{
    public class SubmissionAiReviewPageModel
    {
        public List<Submission> Submissions { get; set; } = new ();

        // SubCategoryId → "Category > Subcategory".
        public Dictionary<int, string> SubcategoryLabels { get; set; } = new ();

        public int AiReviewsToday { get; set; }

        public int DailyCap { get; set; }

        public string? Message { get; set; }

        public string Label(int? subCategoryId) =>
            subCategoryId is int id && this.SubcategoryLabels.TryGetValue(id, out var l) ? l : "(none)";
    }
}
