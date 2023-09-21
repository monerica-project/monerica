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
            this.memoryCache.TryGetValue<List<string>>(CACHEKEY, out var list);

            if (list == null)
            {
                return false;
            }

            return list.Any(e => userAgent.Contains(e));
        }

        private void LoadUserAgentsToCache()
        {
            using var scope = this.serviceProvider.CreateScope();
            var excludeUserAgentRepo = scope.ServiceProvider.GetRequiredService<IExcludeUserAgentRepository>();
            var allExcludedUserAgents = excludeUserAgentRepo.GetAll();
            var excludedUserAgents = allExcludedUserAgents.Select(e => e.UserAgent).ToList();

            this.memoryCache.Set(CACHEKEY, excludedUserAgents);
        }
    }
}
