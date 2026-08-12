namespace DirectoryManager.Web.Models.Submissions
{
    // Backs the optional "help cover our review costs" page at /submission/pay/{token}.
    public class SubmissionPayViewModel
    {
        public Guid Token { get; set; }

        public string Name { get; set; } = string.Empty;

        // A payment was already detected for this submission.
        public bool AlreadyPaid { get; set; }
    }
}
