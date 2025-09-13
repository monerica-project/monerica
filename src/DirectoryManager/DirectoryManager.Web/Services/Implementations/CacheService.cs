// CacheService.cs
using System.Collections.Concurrent;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

public class CacheService : ICacheService
{
    private const string SnippetCachePrefix = "snippet-";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new ();

    private readonly IMemoryCache cache;
    private readonly IServiceScopeFactory scopeFactory;

    public CacheService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        this.cache = cache;
        this.scopeFactory = scopeFactory;
    }

    public void ClearSnippetCache(SiteConfigSetting snippetType)
        => this.cache.Remove(BuildCacheKey(snippetType));

    public async Task<string> GetSnippetAsync(SiteConfigSetting snippetType)
    {
        var cacheKey = BuildCacheKey(snippetType);

        // Fast path: cache hit (no DB, no lock)
        if (this.cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        // Serialize cache MISS per key to avoid concurrent DbContext usage
        var gate = KeyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
            if (this.cache.TryGetValue(cacheKey, out cached) && cached is not null)
                return cached;

            // Fresh scope -> fresh repo -> fresh DbContext for THIS call
            using var scope = this.scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IContentSnippetRepository>();

            var model = await repo.GetAsync(snippetType); // consider AsNoTracking() inside repo
            var content = model?.Content ?? string.Empty;

            this.cache.Set(cacheKey, content, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

            return content;
        }
        finally
        {
            gate.Release();
            // Optional cleanup: remove the semaphore when idle
            if (gate.CurrentCount == 1) KeyLocks.TryRemove(cacheKey, out _);
        }
    }

    private static string BuildCacheKey(SiteConfigSetting snippetType)
        => $"{SnippetCachePrefix}{snippetType}";
}
