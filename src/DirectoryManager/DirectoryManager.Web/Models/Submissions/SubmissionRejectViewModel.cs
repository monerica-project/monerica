using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models.Submissions
{
    /// <summary>The "Reject submission" page: pick a reason (or type one), then confirm.</summary>
    public class SubmissionRejectViewModel
    {
        public Submission Submission { get; set; } = new ();

        public string? From { get; set; }

        public string? Reason { get; set; }

        public string? OtherReason { get; set; }

        public string? Error { get; set; }

        /// <summary>Reason the automated reviewer or its pre-filter gave, shown as a hint.</summary>
        public string? SuggestedByReviewer { get; set; }
    }
}
