using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DirectoryManager.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        private readonly ITrafficLogRepository trafficLogRepository;
        private readonly IUserAgentCacheService userAgentCacheService;

        public BaseController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService)
        {
            this.trafficLogRepository = trafficLogRepository;
            this.userAgentCacheService = userAgentCacheService;
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

            this.trafficLogRepository.AddTrafficLog(trafficLog);
        }
    }
}