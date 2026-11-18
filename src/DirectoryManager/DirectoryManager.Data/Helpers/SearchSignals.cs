namespace DirectoryManager.Data.Helpers
{
    /// <summary>Signals for the whole directory plus the site-wide average rating.</summary>
    public sealed class SearchSignals
    {
        public static readonly SearchSignals None = new ();

        public IReadOnlyDictionary<int, SearchListingSignals> ByEntryId { get; init; } = new Dictionary<int, SearchListingSignals>();

        /// <summary>Average of all approved ratings on the site (the Bayesian prior "C").</summary>
        public double SiteMeanRating { get; init; } = SearchRankingModel.DefaultMeanRating;

        public DateTime NowUtc { get; init; } = DateTime.UtcNow;
    }
}
