using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.ProblemReports;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ProblemReportRepository : IProblemReportRepository
    {
        private readonly IApplicationDbContext context;

        public ProblemReportRepository(IApplicationDbContext context) => this.context = context;

        private DbSet<ProblemReport> Set => this.context.ProblemReports;

        public async Task AddAsync(ProblemReport entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<ProblemReport?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<ProblemReport?> GetByTokenAsync(Guid token, CancellationToken ct = default) =>
            await this.Set.Include(x => x.DirectoryEntry)
                .FirstOrDefaultAsync(x => x.PaymentToken == token, ct);

        public async Task SetInvoiceIdAsync(int id, string invoiceId, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            existing.BtcPayInvoiceId = invoiceId;
            existing.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        public async Task SetPaidAsync(int id, decimal? amount, string? currency, DateTime paidUtc, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null || existing.PaidUtc is not null)
            {
                return;
            }

            existing.PaidUtc = paidUtc;
            existing.PaidAmount = amount;
            existing.PaidCurrency = currency;
            existing.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<List<ProblemReport>> ListByStatusAsync(
            ProblemReportStatus status, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            return await this.Set.AsNoTracking()
                .Where(x => x.Status == status)
                .Include(x => x.DirectoryEntry)
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.ProblemReportId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(ProblemReportStatus status, CancellationToken ct = default) =>
            this.Set.AsNoTracking().Where(x => x.Status == status).CountAsync(ct);

        public Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default) =>
            this.Set.AsNoTracking()
                .OrderByDescending(x => x.CreateDate)
                .Select(x => (DateTime?)x.CreateDate)
                .FirstOrDefaultAsync(ct);

        public Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default) =>
            this.Set.AsNoTracking()
                .Where(x => x.PaidUtc != null)
                .OrderByDescending(x => x.PaidUtc)
                .Select(x => x.PaidUtc)
                .FirstOrDefaultAsync(ct);

        public async Task SetStatusAsync(int id, ProblemReportStatus status, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            existing.Status = status;
            existing.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }
    }
}
