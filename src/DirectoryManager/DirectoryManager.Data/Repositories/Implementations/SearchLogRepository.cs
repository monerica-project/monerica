using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;


namespace DirectoryManager.Data.Repositories.Implementations
{
    /// <inheritdoc />
    public class SearchLogRepository : ISearchLogRepository
    {
        private readonly IApplicationDbContext context;

        public SearchLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc />
        public async Task CreateAsync(SearchLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // stamp the time if not set
            log.CreateDate = log.CreateDate == default
                ? DateTime.UtcNow
                : log.CreateDate;

            await this.context.SearchLogs.AddAsync(log);
            await this.context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<SearchReportItem>> GetReportAsync(DateTime start, DateTime end)
        {
            // 1) filter to range
            var q = this.context.SearchLogs
                .Where(x => x.CreateDate >= start && x.CreateDate < end);

            // 2) total count for percentage
            var total = await q.CountAsync();

            // 3) group and project
            var report = await q
                .GroupBy(x => x.Term)
                .Select(g => new SearchReportItem
                {
                    Term = g.Key!,
                    Count = g.Count(),
                    FirstSearched = g.Min(x => x.CreateDate),
                    LastSearched = g.Max(x => x.CreateDate),
                    Percentage = total == 0
                                  ? 0
                                  : (double)g.Count() * 100.0 / total
                })
                .OrderByDescending(r => r.Count)
                .ToListAsync();

            return report;
        }
    }
}
