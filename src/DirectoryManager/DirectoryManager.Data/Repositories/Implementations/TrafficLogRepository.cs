﻿using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class TrafficLogRepository : ITrafficLogRepository
    {
        private readonly IApplicationDbContext context;

        public TrafficLogRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task AddTrafficLog(TrafficLog trafficLog)
        {
            await this.context.TrafficLogs.AddAsync(trafficLog);
            await this.context.SaveChangesAsync();
        }

        public void DeleteTrafficLog(TrafficLog trafficLog)
        {
            this.context.TrafficLogs.Remove(trafficLog);
            this.context.SaveChanges();
        }

        public int GetUniqueIpsInRange(DateTime start, DateTime end)
        {
            return this.context.TrafficLogs
                .Where(log => log.CreateDate >= start && log.CreateDate <= end)
                .Select(log => log.IpAddress)
                .Distinct()
                .Count();
        }

        public int GetTotalLogsInRange(DateTime start, DateTime end)
        {
            return this.context.TrafficLogs
                .Where(log => log.CreateDate >= start && log.CreateDate <= end)
                .Count();
        }
    }
}