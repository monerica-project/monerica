namespace DirectoryManager.Data.Enums
{
    public enum DirectoryFilterSort
    {
        Newest = 0,
        Oldest = 1,
        HighestRating = 2,
        LowestRating = 3,
        NameAsc = 4,
        NameDesc = 5,
        FoundedDateNewest = 6,
        FoundedDateOldest = 7,
        RecentlyUpdated = 8,
        LeastRecentlyUpdated = 9,

        /// <summary>
        /// Recommended default. Ranks entries that HAVE approved reviews first
        /// (by Bayesian weighted score, then review count), then shows every
        /// remaining unrated entry by newest. Unlike <see cref="HighestRating"/>,
        /// this never hides unrated entries from the list.
        /// </summary>
        TopRated = 10,
    }
}
