using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("trafficreport")]
    public class TrafficReportController : BaseController
    {
        private readonly ITrafficLogRepository trafficLogRepository;

        public TrafficReportController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.trafficLogRepository = trafficLogRepository;
        }

        [HttpGet("last24hours")]
        public IActionResult Last24Hours()
        {
            return this.Index(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        }

        [HttpGet("lastweek")]
        public IActionResult LastWeek()
        {
            return this.Index(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        }

        [HttpGet("lastmonth")]
        public IActionResult LastMonth()
        {
            return this.Index(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        }

        [HttpGet("index")]
        public IActionResult Index(DateTime? start, DateTime? end)
        {
            if (!start.HasValue)
            {
                start = DateTime.UtcNow.AddDays(-30);
            }

            if (!end.HasValue)
            {
                end = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
            }

            end = new DateTime(end.Value.Year, end.Value.Month, end.Value.Day, 23, 59, 59, DateTimeKind.Utc);

            var uniqueIPs = this.trafficLogRepository.GetUniqueIpsInRange(start.Value, end.Value);
            var totalLogs = this.trafficLogRepository.GetTotalLogsInRange(start.Value, end.Value);

            var model = new TrafficReportViewModel
            {
                StartDate = start.Value,
                EndDate = end.Value,
                UniqueIPCount = uniqueIPs,
                TotalLogCount = totalLogs
            };

            return this.View("index", model);
        }
    }
}
