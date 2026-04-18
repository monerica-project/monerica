using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SearchLogRepository : ISearchLogRepository
    {
        private readonly IApplicationDbContext context;

        public SearchLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

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

        public async Task<IReadOnlyList<WeeklySearchCount>> GetWeeklyCountsAsync(DateTime start, DateTime end)
        {
            // Treat input as UTC dates; include the whole end day
            DateTime startUtc = start.Kind == DateTimeKind.Utc ? start : DateTime.SpecifyKind(start, DateTimeKind.Utc);
            DateTime endUtc = end.Kind == DateTimeKind.Utc ? end : DateTime.SpecifyKind(end, DateTimeKind.Utc);

            DateTime endExclusive = endUtc.AddDays(1); // include end day fully

            // Pull just the timestamps in range
            var dates = await this.context.SearchLogs
                .Where(x => x.CreateDate >= startUtc && x.CreateDate < endExclusive)
                .Select(x => x.CreateDate)
                .ToListAsync();

            // Normalize to UTC date and group by ISO-week (Monday start)
            static DateTime ToUtcDate(DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Utc)
                {
                    return dt.Date;
                }

                if (dt.Kind == DateTimeKind.Local)
                {
                    return dt.ToUniversalTime().Date;
                }

                return DateTime.SpecifyKind(dt, DateTimeKind.Utc).Date;
            }

            static DateTime StartOfIsoWeek(DateTime dUtcDate)
            {
                int diff = ((int)dUtcDate.DayOfWeek + 6) % 7; // Monday=0 .. Sunday=6
                return dUtcDate.AddDays(-diff).Date;
            }

            var buckets = dates
                .Select(ToUtcDate)
                .GroupBy(d => StartOfIsoWeek(d))
                .Select(g => new WeeklySearchCount
                {
                    WeekStartUtc = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.WeekStartUtc)
                .ToList();

            return buckets;
        }
    }
}