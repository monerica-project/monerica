using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // =========================================================
        // ✅ ORDER FOR THREAD DISPLAY (chronological)
        // Oldest -> newest:
        // ORDER BY CreateDate ASC, CommentId ASC
        // =========================================================
        private static IOrderedQueryable<DirectoryEntryReviewComment> ApplyThreadOrder(IQueryable<DirectoryEntryReviewComment> q)
        {
            return q
                .OrderBy(c => c.CreateDate)
                .ThenBy(c => c.DirectoryEntryReviewCommentId);
        }

        private IQueryable<DirectoryEntryReviewComment> ApprovedForReviewQuery(int directoryEntryReviewId)
        {
            return this.Set.AsNoTracking()
                .Where(c =>
                    c.DirectoryEntryReviewId == directoryEntryReviewId &&
                    c.ModerationStatus == ReviewModerationStatus.Approved);
        }

        // =========================================================
        // ✅ Latest replies (homepage list)
        // This list can be newest-first, because it's a "latest" feed.
        // =========================================================
        public async Task<IReadOnlyList<DirectoryEntryReviewComment>> ListLatestApprovedAsync(int take)
        {
            if (take <= 0)
            {
                return Array.Empty<DirectoryEntryReviewComment>();
            }

            return await this.context.DirectoryEntryReviewComments
                .AsNoTracking()
                .Where(c =>
                    c.DirectoryEntryReview != null &&
                    c.DirectoryEntryReview.ModerationStatus == ReviewModerationStatus.Approved &&
                    c.ModerationStatus == ReviewModerationStatus.Approved &&
                    c.DirectoryEntryReview.DirectoryEntry != null &&
                    c.DirectoryEntryReview.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Include(c => c.DirectoryEntryReview)
                    .ThenInclude(r => r.DirectoryEntry)
                        .ThenInclude(e => e.SubCategory)
                            .ThenInclude(sc => sc.Category)
                .OrderByDescending(c => c.UpdateDate ?? c.CreateDate)
                .ThenByDescending(c => c.DirectoryEntryReviewCommentId)
                .Take(take)
                .ToListAsync();
        }

        // =========================================================
        // ✅ For sitemap / "last modified" per entry (approved replies)
        // =========================================================
        public async Task<Dictionary<int, DateTime>> GetApprovedReplyLastModifiedByEntryAsync(CancellationToken ct = default)
        {
            return await (
                from c in this.context.DirectoryEntryReviewComments.AsNoTracking()
                join r in this.context.DirectoryEntryReviews.AsNoTracking()
                    on c.DirectoryEntryReviewId equals r.DirectoryEntryReviewId
                where c.ModerationStatus == ReviewModerationStatus.Approved
                      && r.ModerationStatus == ReviewModerationStatus.Approved
                group c.UpdateDate ?? c.CreateDate by r.DirectoryEntryId into g
                select new
                {
                    EntryId = g.Key,
                    Last = g.Max()
                }
            ).ToDictionaryAsync(x => x.EntryId, x => x.Last, ct);
        }

        // ---------------------------
        // CRUD
        // ---------------------------
        public async Task AddAsync(DirectoryEntryReviewComment entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
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
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewCommentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default)
            => this.Set.CountAsync(ct);

        // ---------------------------
        // Listing helpers (per review)
        // ---------------------------

        // ✅ Existing: full thread (chronological)
        public async Task<List<DirectoryEntryReviewComment>> ListApprovedForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default)
        {
            var q = ApplyThreadOrder(this.ApprovedForReviewQuery(directoryEntryReviewId));
            return await q.ToListAsync(ct);
        }

        // ✅ NEW: paged thread (chronological)
        // This is what you need to render the "rcp=" deep link page.
        public async Task<List<DirectoryEntryReviewComment>> ListApprovedForReviewAsync(
            int directoryEntryReviewId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            var q = ApplyThreadOrder(this.ApprovedForReviewQuery(directoryEntryReviewId));

            return await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> CountApprovedForReviewAsync(int directoryEntryReviewId, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(c =>
                    c.DirectoryEntryReviewId == directoryEntryReviewId &&
                    c.ModerationStatus == ReviewModerationStatus.Approved)
                .CountAsync(ct);
        }

        // Existing: all comments for a review (any status) chronological
        public async Task<List<DirectoryEntryReviewComment>> ListForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default)
        {
            var q = ApplyThreadOrder(
                this.Set.AsNoTracking().Where(c => c.DirectoryEntryReviewId == directoryEntryReviewId));

            return await q.ToListAsync(ct);
        }

        public async Task<List<DirectoryEntryReviewComment>> ListByStatusAsync(
            ReviewModerationStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .Where(c => c.ModerationStatus == status)
                .OrderBy(x => x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewCommentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(ReviewModerationStatus status, CancellationToken ct = default)
            => this.Set.Where(c => c.ModerationStatus == status).CountAsync(ct);

        // =========================================================
        // ✅ Comment page map (chronological)
        //
        // For a set of commentIds, returns:
        //   commentId -> pageNumber (within its parent review thread)
        //
        // Thread order:
        //   CreateDate ASC, CommentId ASC
        //
        // position = count(approved comments where date < c.date OR (date== AND id<=))
        // page = ceil(position / pageSize)
        // =========================================================
        public async Task<Dictionary<int, int>> GetApprovedCommentPageMapAsync(
            IEnumerable<int> commentIds,
            int pageSize,
            CancellationToken ct = default)
        {
            var ids = (commentIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            if (pageSize < 1)
            {
                pageSize = 25;
            }

            // Pull the target comments (need reviewId + createDate for comparator)
            var rows = await this.Set.AsNoTracking()
                .Where(c => ids.Contains(c.DirectoryEntryReviewCommentId)
                         && c.ModerationStatus == ReviewModerationStatus.Approved)
                .Select(c => new
                {
                    c.DirectoryEntryReviewCommentId,
                    c.DirectoryEntryReviewId,
                    c.CreateDate,
                    Position = this.Set.AsNoTracking()
                        .Where(x => x.DirectoryEntryReviewId == c.DirectoryEntryReviewId
                                 && x.ModerationStatus == ReviewModerationStatus.Approved
                                 && (
                                     x.CreateDate < c.CreateDate
                                     || (x.CreateDate == c.CreateDate
                                         && x.DirectoryEntryReviewCommentId <= c.DirectoryEntryReviewCommentId)
                                 ))
                        .Count()
                })
                .ToListAsync(ct);

            var result = new Dictionary<int, int>(rows.Count);

            foreach (var row in rows)
            {
                int page = (int)Math.Ceiling(row.Position / (double)pageSize);
                if (page < 1) page = 1;

                result[row.DirectoryEntryReviewCommentId] = page;
            }

            return result;
        }

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
