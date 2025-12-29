// DirectoryManager.Data/Repositories/Implementations/DirectoryEntryReviewRepository.cs
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryReviewRepository : IDirectoryEntryReviewRepository
    {
        private readonly IApplicationDbContext context;
        public DirectoryEntryReviewRepository(IApplicationDbContext context) => this.context = context;

        private DbSet<DirectoryEntryReview> Set => this.context.DirectoryEntryReviews;

        public IQueryable<DirectoryEntryReview> Query() => this.Set.AsNoTracking();

        public async Task<DirectoryEntryReview?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<List<DirectoryEntryReview>> ListAsync(
            int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task AddAsync(DirectoryEntryReview entity, CancellationToken ct = default)
        {
            // Ensure required model invariants from your entity:
            // AuthorFingerprint is [Required] (uppercase, no spaces) – callers should normalize it.
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null; // first insert
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(DirectoryEntryReview entity, CancellationToken ct = default)
        {
            entity.UpdateDate = DateTime.UtcNow;
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            this.Set.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }

        // ---------------------------
        // Public-side helpers (approved only)
        // ---------------------------
        public async Task<List<DirectoryEntryReview>> ListApprovedForEntryAsync(
            int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId &&
                            r.ModerationStatus == ReviewModerationStatus.Approved)
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public async Task<double?> AverageRatingForEntryApprovedAsync(
            int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId &&
                            r.ModerationStatus == ReviewModerationStatus.Approved &&
                            r.Rating.HasValue)
                .Select(r => (double)r.Rating!.Value);

            if (!await q.AnyAsync(ct))
            {
                return null;
            }

            return await q.AverageAsync(ct);
        }

        public async Task<List<DirectoryEntryReview>> ListByStatusAsync(
            ReviewModerationStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == status)
                .OrderBy(r => r.CreateDate) // oldest first for queue processing
                .ThenBy(r => r.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public Task<int> CountByStatusAsync(
            ReviewModerationStatus status, CancellationToken ct = default) =>
            this.Set.Where(r => r.ModerationStatus == status).CountAsync(ct);

        public async Task SetModerationStatusAsync(
            int id, ReviewModerationStatus status, string reason, CancellationToken ct = default)
        {
            var review = await this.Set.FindAsync(new object[] { id }, ct);
            if (review is null)
            {
                return;
            }

            review.RejectionReason = reason;
            review.ModerationStatus = status;
            review.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<List<DirectoryEntryReview>> ListLatestApprovedAsync(
            int count = 10, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                    && r.DirectoryEntry != null
                    && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Include(r => r.DirectoryEntry)
                    .ThenInclude(de => de.SubCategory!)
                        .ThenInclude(sc => sc.Category!)
                .OrderByDescending(r => r.UpdateDate ?? r.CreateDate)
                .ThenBy(r => r.DirectoryEntryReviewId)
                .Take(count)
                .ToListAsync(ct);

        public Task ApproveAsync(int id, CancellationToken ct = default) =>
            this.SetModerationStatusAsync(id, ReviewModerationStatus.Approved, string.Empty, ct);

        public Task RejectAsync(int id, string reason, CancellationToken ct = default) =>
            this.SetModerationStatusAsync(id, ReviewModerationStatus.Rejected, reason, ct);

        public async Task<List<DirectoryEntryReview>> ListForEntryAsync(
            int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId)
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public async Task<double?> AverageRatingForEntryAsync(
            int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId && r.Rating.HasValue)
                .Select(r => (double)r.Rating!.Value);

            if (!await q.AnyAsync(ct))
            {
                return null;
            }

            return await q.AverageAsync(ct);
        }

        public async Task<Dictionary<int, DateTime>> GetLatestApprovedReviewDatesByEntryAsync(CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new
                {
                    DirectoryEntryId = g.Key,
                    Last = g.Max(r => r.UpdateDate ?? r.CreateDate)
                })
                .ToDictionaryAsync(x => x.DirectoryEntryId, x => x.Last, ct);
        }
    }
}
