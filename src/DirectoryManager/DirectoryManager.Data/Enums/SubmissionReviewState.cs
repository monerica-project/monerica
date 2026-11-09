namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Where a submission is in the automated (AI-assisted) review. The reviewer never publishes:
    /// every state except <see cref="Approved"/> / <see cref="Rejected"/> waits for an admin.
    /// </summary>
    public enum SubmissionReviewState
    {
        /// <summary>Not reviewed yet (or queued again with "Re-run review").</summary>
        Pending = 0,

        /// <summary>The code-only pre-filter found a dead URL, a duplicate or spam. An admin confirms the reject.</summary>
        AutoRejected = 1,

        /// <summary>Needs a closer look (banned category, unsure verdict, review error).</summary>
        Flagged = 2,

        /// <summary>The AI found clear evidence; the suggested version is ready to approve.</summary>
        AiApproveCandidate = 3,

        /// <summary>The AI recommends rejecting.</summary>
        AiRejectCandidate = 4,

        /// <summary>An admin approved it (the listing was created/updated).</summary>
        Approved = 5,

        /// <summary>An admin rejected it.</summary>
        Rejected = 6,

        /// <summary>Not something the reviewer handles (e.g. a site-checker status proposal).</summary>
        Skipped = 7,
    }
}
