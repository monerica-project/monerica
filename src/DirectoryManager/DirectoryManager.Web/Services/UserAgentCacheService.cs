using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services
{
    public class UserAgentCacheService
    {
        private const string CACHEKEY = "ExcludedUserAgents";
        private readonly IMemoryCache memoryCache;
        private readonly IServiceProvider serviceProvider;

        public UserAgentCacheService(IServiceProvider serviceProvider, IMemoryCache memoryCache)
        {
            this.serviceProvider = serviceProvider;
            this.memoryCache = memoryCache;
            this.LoadUserAgentsToCache();
        }

        public bool IsUserAgentExcluded(string userAgent)
        {
            return this.memoryCache.TryGetValue<List<string>>(CACHEKEY, out var excludedUserAgents)
                && (excludedUserAgents != null && excludedUserAgents.Contains(userAgent));
        }

        private void LoadUserAgentsToCache()
        {
            using var scope = this.serviceProvider.CreateScope();
            var excludeUserAgentRepo = scope.ServiceProvider.GetRequiredService<IExcludeUserAgentRepository>();
            var allExcludedUserAgents = excludeUserAgentRepo.GetAll();

            this.memoryCache.Set(CACHEKEY, allExcludedUserAgents);
        }
    }
}
