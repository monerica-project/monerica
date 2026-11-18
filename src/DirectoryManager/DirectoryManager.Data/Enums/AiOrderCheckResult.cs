namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// What the AI order check found for a review's order link. Advisory only: it never changes a
    /// review's moderation status.
    /// </summary>
    public enum AiOrderCheckResult
    {
        /// <summary>Not looked at yet (or no order link).</summary>
        NotChecked = 0,

        /// <summary>The link shows a real, finished order on the listing's own site that fits the review.</summary>
        ChecksOut = 1,

        /// <summary>The link shows no such order, a different site, a failed order, or a reused link.</summary>
        DoesNotCheckOut = 2,

        /// <summary>The page could not be read or does not show enough; a person has to look.</summary>
        CouldNotVerify = 3,

        /// <summary>The order exists but is still in progress; it is looked at again later.</summary>
        InProgress = 4,
    }
}
