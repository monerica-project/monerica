using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        private readonly ITrafficLogRepository trafficLogRepository;
        private readonly IUserAgentCacheService userAgentCacheService;
        private readonly IMemoryCache cache;

        public BaseController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
        {
            this.trafficLogRepository = trafficLogRepository;
            this.userAgentCacheService = userAgentCacheService;
            this.cache = cache;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            try
            {
                if (this.User != null &&
                    this.User.Identity != null &&
                    this.User.Identity.IsAuthenticated)
                {
                    return;
                }

                var ipAddress = this.HttpContext.GetRemoteIpIfEnabled();

                if (ipAddress == null)
                {
                    return;
                }

                var url = context.HttpContext.Request.Path.ToString();
                var userAgent = context.HttpContext.Request.Headers[StringConstants.UserAgent].ToString();

                if (ipAddress == null)
                {
                    return;
                }

                if (this.userAgentCacheService.IsUserAgentExcluded(userAgent))
                {
                    return;
                }

                var trafficLog = new TrafficLog
                {
                    IpAddress = ipAddress,
                    Url = url,
                    UserAgent = userAgent
                };

                await this.trafficLogRepository.AddTrafficLog(trafficLog);
            }
            finally
            {
                await base.OnActionExecutionAsync(context, next);
            }
        }

        protected void ClearCachedItems()
        {
            CachePrefixManager.ExpirePrefix(StringConstants.DirectoryFilterCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveSubcategoriesByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveTagsByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveTagIdsByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.CacheKeyPrefixConversion);

            this.cache.Remove(StringConstants.CacheKeyEntries);
            this.cache.Remove(StringConstants.CacheKeySponsoredListings);
            this.cache.Remove(StringConstants.CacheKeyAllActiveSponsors);
            this.cache.Remove(StringConstants.NavMenu);
        }
    }
}