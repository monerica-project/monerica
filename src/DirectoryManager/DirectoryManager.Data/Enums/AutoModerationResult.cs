namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// What the automated review-moderation worker decided on a given pass. Stored on
    /// <c>DirectoryEntryReview.AutoModerationResult</c> so an automatic action is always
    /// distinguishable from a human one (which leaves this at <see cref="None"/>).
    /// </summary>
    public enum AutoModerationResult
    {
        /// <summary>Never auto-processed (manual / not yet evaluated).</summary>
        None = 0,

        /// <summary>Worker confirmed a completed order and approved it.</summary>
        AutoApproved = 1,

        /// <summary>Worker confirmed the order is in a terminal-failure state and rejected it.</summary>
        AutoRejected = 2,

        /// <summary>Worker could not decide confidently and handed it to a human.</summary>
        Flagged = 3,
    }
}
