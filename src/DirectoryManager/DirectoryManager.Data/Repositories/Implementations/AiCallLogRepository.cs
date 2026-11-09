using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class AiCallLogRepository : IAiCallLogRepository
    {
        private readonly IApplicationDbContext context;

        public AiCallLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<(IReadOnlyList<AiCallLog> Items, int Total)> GetPageAsync(int page, int pageSize, int? submissionId, bool errorsOnly, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = this.context.AiCallLogs.AsNoTracking();
            if (submissionId is int sid)
            {
                q = q.Where(x => x.SubmissionId == sid);
            }

            if (errorsOnly)
            {
                q = q.Where(x => x.Error != null);
            }

            var total = await q.CountAsync(ct);

            // The list never needs the large request/response texts.
            var items = await q.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new AiCallLog
                {
                    Id = x.Id,
                    CreatedUtc = x.CreatedUtc,
                    Feature = x.Feature,
                    SubmissionId = x.SubmissionId,
                    SubjectName = x.SubjectName,
                    Model = x.Model,
                    Attempts = x.Attempts,
                    HttpStatus = x.HttpStatus,
                    DurationMs = x.DurationMs,
                    InputTokens = x.InputTokens,
                    OutputTokens = x.OutputTokens,
                    StopReason = x.StopReason,
                    Verdict = x.Verdict,
                    Confidence = x.Confidence,
                    Summary = x.Summary,
                    Outcome = x.Outcome,
                    Error = x.Error,
                })
                .ToListAsync(ct);
            return (items, total);
        }

        public Task<AiCallLog?> GetByIdAsync(int id, CancellationToken ct = default) =>
            this.context.AiCallLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<IReadOnlyList<AiCallDailyTotal>> GetDailyTotalsAsync(int days, CancellationToken ct = default)
        {
            var since = DateTime.UtcNow.Date.AddDays(-(Math.Max(1, days) - 1));
            var rows = await this.context.AiCallLogs.AsNoTracking()
                .Where(x => x.CreatedUtc >= since)
                .Select(x => new { x.CreatedUtc, x.Error, x.InputTokens, x.OutputTokens })
                .ToListAsync(ct);
            return rows.GroupBy(x => x.CreatedUtc.Date)
                .Select(g => new AiCallDailyTotal(g.Key, g.Count(), g.Count(x => x.Error != null), g.Sum(x => (long)x.InputTokens), g.Sum(x => (long)x.OutputTokens)))
                .OrderByDescending(x => x.Day)
                .ToList();
        }
    }
}
