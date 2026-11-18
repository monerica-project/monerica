namespace DirectoryManager.Data.Enums
{
    /// <summary>Outcome of the monthly terms check for one listing.</summary>
    public enum TermsWatchResult
    {
        /// <summary>First time the listing's pages were read: stored as the starting point, nothing to compare.</summary>
        Baseline = 0,

        /// <summary>The policy wording is the same as last time.</summary>
        Unchanged = 1,

        /// <summary>The wording changed, the AI compared it with the listing and found nothing to correct.</summary>
        ChangedNoAction = 2,

        /// <summary>The wording changed and the listing's data probably needs correcting.</summary>
        Flagged = 3,

        /// <summary>The wording changed but the AI budget was used up (or the AI call failed): a person should look.</summary>
        ChangedNotReviewed = 4,

        /// <summary>The site could not be read this time (down, blocked, or needs JavaScript).</summary>
        CouldNotRead = 5,
    }
}
