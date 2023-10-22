﻿using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
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

        [HttpGet]
        public IActionResult Index(DateTime? start, DateTime? end)
        {
            if (!start.HasValue)
            {
                start = DateTime.UtcNow.AddDays(-30);
            }

            if (!end.HasValue)
            {
                end = DateTime.UtcNow;
            }

            var uniqueIPs = this.trafficLogRepository.GetUniqueIpsInRange(start.Value, end.Value);
            var totalLogs = this.trafficLogRepository.GetTotalLogsInRange(start.Value, end.Value);

            var model = new TrafficReportViewModel
            {
                StartDate = start.Value,
                EndDate = end.Value,
                UniqueIPCount = uniqueIPs,
                TotalLogCount = totalLogs
            };

            return this.View(model);
        }
    }
}
