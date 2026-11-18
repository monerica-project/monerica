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

        /// <summary>Set when <see cref="Message"/> reports something that was NOT done.</summary>
        public bool MessageIsProblem { get; set; }

        // SubmissionId → changes that one-click approval refuses (links, contacts, PGP key, status).
        // Such an edit can only be approved on the full review form.
        public Dictionary<int, List<string>> SensitiveChanges { get; set; } = new ();

        public List<string> Sensitive(int submissionId) =>
            this.SensitiveChanges.TryGetValue(submissionId, out var l) ? l : new List<string>();

        public string Label(int? subCategoryId) =>
            subCategoryId is int id && this.SubcategoryLabels.TryGetValue(id, out var l) ? l : "(none)";
    }
}
