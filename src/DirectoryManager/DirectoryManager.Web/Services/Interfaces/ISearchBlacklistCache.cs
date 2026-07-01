namespace DirectoryManager.Web.Services.Interfaces
{
    public interface ISearchBlacklistCache
    {
        /// <summary>
        /// Returns normalized blacklist terms (trimmed + lowercased).
        /// Cached via IMemoryCache using StringConstants.CacheKeySearchBlacklistTerms.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<HashSet<string>> GetTermsAsync(CancellationToken ct = default);

        /// <summary>
        /// Clears the cached terms so changes in admin UI take effect quickly.
        /// </summary>
        void Invalidate();
    }
}
