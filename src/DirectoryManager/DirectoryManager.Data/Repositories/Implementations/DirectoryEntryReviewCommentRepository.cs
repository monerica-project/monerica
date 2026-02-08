using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryReviewCommentRepository : IDirectoryEntryReviewCommentRepository
    {
        private readonly IApplicationDbContext context;

        public DirectoryEntryReviewCommentRepository(IApplicationDbContext context)
            => this.context = context;

        private DbSet<DirectoryEntryReviewComment> Set => this.context.DirectoryEntryReviewComments;

        public IQueryable<DirectoryEntryReviewComment> Query() => this.Set.AsNoTracking();

        public async Task<DirectoryEntryReviewComment?> GetByIdAsync(int id, CancellationToken ct = default)
            => await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<IReadOnlyList<DirectoryEntryReviewComment>> ListLatestApprovedAsync(int take)
        {
            // Safety
            if (take <= 0)
            {
                return Array.Empty<DirectoryEntryReviewComment>();
            }

            // NOTE:
            // - Adjust the "Approved" predicate below to match your actual schema.
            // - The Include() chain is so the homepage can show:
            //   comment -> review -> entry -> subcategory -> category (and not lazy-load)
            return await this.context.DirectoryEntryReviewComments
                .AsNoTracking()
                .Where(c =>
                    c.DirectoryEntryReview != null &&
                    c.DirectoryEntryReview.ModerationStatus == ReviewModerationStatus.Approved)
                .Include(c => c.DirectoryEntryReview)
                    .ThenInclude(r => r.DirectoryEntry)
                        .ThenInclude(e => e.SubCategory)
                            .ThenInclude(sc => sc.Category)
                .OrderByDescending(c => c.UpdateDate ?? c.CreateDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task AddAsync(DirectoryEntryReviewComment entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null; // first insert
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(DirectoryEntryReviewComment entity, CancellationToken ct = default)
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
        // Global list + counts (moderation dashboard)
        // ---------------------------
        public async Task<List<DirectoryEntryReviewComment>> ListAsync(
            int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewCommentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default)
            => this.Set.CountAsync(ct);

        // ---------------------------
        // Listing helpers (per review)
        // ---------------------------
        public async Task<List<DirectoryEntryReviewComment>> ListApprovedForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(c =>
                    c.DirectoryEntryReviewId == directoryEntryReviewId &&
                    c.ModerationStatus == ReviewModerationStatus.Approved)
                .OrderBy(x => x.CreateDate) // thread reads better oldest->newest
                .ThenBy(x => x.DirectoryEntryReviewCommentId)
                .ToListAsync(ct);
        }

        public async Task<List<DirectoryEntryReviewComment>> ListForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(c => c.DirectoryEntryReviewId == directoryEntryReviewId)
                .OrderBy(x => x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewCommentId)
                .ToListAsync(ct);
        }

        public async Task<List<DirectoryEntryReviewComment>> ListByStatusAsync(
            ReviewModerationStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(c => c.ModerationStatus == status)
                .OrderBy(x => x.CreateDate) // oldest first for queue processing
                .ThenBy(x => x.DirectoryEntryReviewCommentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(ReviewModerationStatus status, CancellationToken ct = default)
            => this.Set.Where(c => c.ModerationStatus == status).CountAsync(ct);

        // ---------------------------
        // Moderation helpers
        // ---------------------------
        public async Task SetModerationStatusAsync(
            int id, ReviewModerationStatus status, string reason, CancellationToken ct = default)
        {
            var comment = await this.Set.FindAsync(new object[] { id }, ct);
            if (comment is null)
            {
                return;
            }

            comment.RejectionReason = reason;
            comment.ModerationStatus = status;
            comment.UpdateDate = DateTime.UtcNow;

            await this.context.SaveChangesAsync(ct);
        }

        public Task ApproveAsync(int id, CancellationToken ct = default)
            => this.SetModerationStatusAsync(id, ReviewModerationStatus.Approved, string.Empty, ct);

        public Task RejectAsync(int id, string reason, CancellationToken ct = default)
            => this.SetModerationStatusAsync(id, ReviewModerationStatus.Rejected, reason, ct);
    }
}
