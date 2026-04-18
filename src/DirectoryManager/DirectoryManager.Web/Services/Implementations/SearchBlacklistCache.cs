using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services
{
    public class SearchBlacklistCache : ISearchBlacklistCache
    {
        private readonly IMemoryCache memoryCache;
        private readonly ISearchBlacklistRepository blacklistRepository;

        public SearchBlacklistCache(IMemoryCache memoryCache, ISearchBlacklistRepository blacklistRepository)
        {
            this.memoryCache = memoryCache;
            this.blacklistRepository = blacklistRepository;
        }

        public async Task<HashSet<string>> GetTermsAsync(CancellationToken ct = default)
        {
            if (this.memoryCache.TryGetValue(StringConstants.CacheKeySearchBlacklistTermsCacheKey, out HashSet<string>? set) && set is not null)
            {
                return set;
            }

            var terms = await this.blacklistRepository.GetAllTermsAsync();

            var norm = new HashSet<string>(
                terms
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            _ = this.memoryCache.Set(
                StringConstants.CacheKeySearchBlacklistTermsCacheKey,
                norm,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6)
                });

            return norm;
        }

        public void Invalidate()
        {
            this.memoryCache.Remove(StringConstants.CacheKeySearchBlacklistTermsCacheKey);
        }
    }
}