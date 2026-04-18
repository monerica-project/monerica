using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryReviewRaffleEntryRepository : IDirectoryEntryReviewRaffleEntryRepository
    {
        // Statuses that mean "the author already has a live raffle entry" and cannot enter again.
        private static readonly RaffleEntryStatus[] ActiveStatuses =
        [
            RaffleEntryStatus.Pending,
            RaffleEntryStatus.Eligible
        ];

        private readonly IApplicationDbContext context;

        public DirectoryEntryReviewRaffleEntryRepository(IApplicationDbContext context) =>
            this.context = context;

        private DbSet<DirectoryEntryReviewRaffleEntry> Set => this.context.DirectoryEntryReviewRaffleEntries;

        public async Task<DirectoryEntryReviewRaffleEntry?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<DirectoryEntryReviewRaffleEntry?> GetByReviewIdAsync(int directoryEntryReviewId, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DirectoryEntryReviewId == directoryEntryReviewId, ct);

        /// <inheritdoc />
        public async Task<DirectoryEntryReviewRaffleEntry?> GetActiveEntryByFingerprintAsync(
            string fingerprint, CancellationToken ct = default)
        {
            fingerprint = (fingerprint ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return null;
            }

            return await this.Set.AsNoTracking()
                .Include(x => x.DirectoryEntryReview)
                .Where(x =>
                    x.DirectoryEntryReview.AuthorFingerprint == fingerprint &&
                    ActiveStatuses.Contains(x.Status))
                .OrderByDescending(x => x.CreateDate)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<DirectoryEntryReviewRaffleEntry>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewRaffleEntryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<List<DirectoryEntryReviewRaffleEntry>> ListByStatusAsync(
            RaffleEntryStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .Where(x => x.Status == status)
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewRaffleEntryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public Task<int> CountByStatusAsync(RaffleEntryStatus status, CancellationToken ct = default) =>
            this.Set.Where(x => x.Status == status).CountAsync(ct);

        public async Task AddAsync(DirectoryEntryReviewRaffleEntry entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(DirectoryEntryReviewRaffleEntry entity, CancellationToken ct = default)
        {
            entity.UpdateDate = DateTime.UtcNow;
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null) return;

            this.Set.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task SetStatusAsync(int id, RaffleEntryStatus status, CancellationToken ct = default)
        {
            var entry = await this.Set.FindAsync(new object[] { id }, ct);
            if (entry is null) return;

            entry.Status = status;
            entry.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }
    }
}
