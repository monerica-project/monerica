using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    public class CacheService : ICacheService
    {
        private const string SnippetCachePrefix = "snippet-";
        private readonly IContentSnippetRepository contentSnippetRepository;
        private IMemoryCache memoryCache;

        public CacheService(
            IMemoryCache memoryCache,
            IContentSnippetRepository contentSnippetRepository)
        {
            this.memoryCache = memoryCache;
            this.contentSnippetRepository = contentSnippetRepository;
        }

        public void ClearSnippetCache(SiteConfigSetting snippetType)
        {
            var cacheKey = this.BuildCacheKey(snippetType);

            this.memoryCache.Remove(cacheKey);
        }

        public string GetSnippet(SiteConfigSetting snippetType)
        {
            var cacheKey = this.BuildCacheKey(snippetType);

            if (this.memoryCache.TryGetValue(cacheKey, out string? maybeSnippet) && maybeSnippet != null)
            {
                return maybeSnippet;
            }
            else
            {
                var dbModel = this.contentSnippetRepository.Get(snippetType);

                var content = dbModel?.Content ?? string.Empty;
                if (dbModel != null)
                {
                    this.memoryCache.Set(cacheKey, content);
                }

                return content;
            }
        }

        private string BuildCacheKey(SiteConfigSetting snippetType)
        {
            return string.Format("{0}{1}", SnippetCachePrefix, snippetType.ToString());
        }
    }
}