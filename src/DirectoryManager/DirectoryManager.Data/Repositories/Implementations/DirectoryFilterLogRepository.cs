using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryFilterLogRepository : IDirectoryFilterLogRepository
    {
        private readonly IApplicationDbContext context;

        public DirectoryFilterLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task CreateAsync(DirectoryFilterLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            log.CreateDate = log.CreateDate == default ? DateTime.UtcNow : log.CreateDate;

            await this.context.DirectoryFilterLogs.AddAsync(log);
            await this.context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<DirectoryFilterLog>> GetInRangeAsync(DateTime start, DateTime end)
        {
            return await this.context.DirectoryFilterLogs
                .Where(x => x.CreateDate >= start && x.CreateDate < end)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
