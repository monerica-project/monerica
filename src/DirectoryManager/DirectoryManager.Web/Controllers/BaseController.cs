using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
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

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (this.User != null &&
                this.User.Identity != null &&
                this.User.Identity.IsAuthenticated)
            {
                return;
            }

            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
            var url = context.HttpContext.Request.Path.ToString();
            var userAgent = context.HttpContext.Request.Headers["User-Agent"].ToString();

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

            Task.Run(async () => await this.trafficLogRepository.AddTrafficLogAsync(trafficLog));
        }

        protected void ClearCachedItems()
        {
            this.cache.Remove(StringConstants.EntriesCacheKey);
            this.cache.Remove(StringConstants.SponsoredListingsCacheKey);
        }
    }
}