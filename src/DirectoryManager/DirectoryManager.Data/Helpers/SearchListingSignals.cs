namespace DirectoryManager.Data.Helpers
{
    /// <summary>Per-listing facts used for ranking, collected once when the search index is built.</summary>
    public sealed class SearchListingSignals
    {
        /// <summary>Approved reviews that have a star rating.</summary>
        public int ReviewCount { get; init; }

        /// <summary>Average star rating (1-5) of those reviews; 0 when there are none.</summary>
        public double AverageRating { get; init; }

        public DateTime? LastReviewUtc { get; init; }

        /// <summary>Problem reports nobody has reviewed or dismissed yet.</summary>
        public int OpenProblemReports { get; init; }

        public bool HasDepositGuarantee { get; init; }
    }
}
