namespace DirectoryManager.Web.Models.Submissions
{
    /// <summary>The "Submit a link" form in the admin portal.</summary>
    public class SubmissionFromLinkModel
    {
        public string? Link { get; set; }

        /// <summary>Optional context for the review (never shown publicly).</summary>
        public string? Note { get; set; }

        public string? Problem { get; set; }

        public string? ProblemUrl { get; set; }

        public string? ProblemUrlText { get; set; }
    }
}
