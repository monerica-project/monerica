using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
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

        // =========================================================
        // ✅ SINGLE SOURCE OF TRUTH FOR APPROVED LISTING ORDER
        //
        // Page 1 = NEWEST reviews
        // ORDER BY CreateDate DESC, DirectoryEntryReviewId DESC
        //
        // IMPORTANT:
        // - This ordering MUST match GetApprovedReviewPageMapAsync EXACTLY
        // - If you ever change this ordering, update page-map predicates too.
        // =========================================================

        private IQueryable<DirectoryEntryReview> ApprovedForEntryQuery(int directoryEntryId)
        {
            return this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved);
        }

        private static IOrderedQueryable<DirectoryEntryReview> ApplyApprovedListingOrder(IQueryable<DirectoryEntryReview> q)
        {
            return q
                .OrderByDescending(r => r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId);
        }

        // ---------------------------
        // CRUD / admin
        // ---------------------------
        public async Task<DirectoryEntryReview?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<List<DirectoryEntryReview>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            // admin list (your preference)
            return await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task AddAsync(DirectoryEntryReview entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
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
            if (existing is null) return;

            this.Set.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }

        // ---------------------------
        // Public-side helpers (approved only)
        // ---------------------------

        // ✅ Used by /site/{key}/page/{n}
        public async Task<List<DirectoryEntryReview>> ListApprovedForEntryAsync(
            int directoryEntryId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            // IMPORTANT: declare as IQueryable so it can hold Include + ordered query
            IQueryable<DirectoryEntryReview> q = this.ApprovedForEntryQuery(directoryEntryId)
                .Include(r => r.ReviewTags)
                    .ThenInclude(x => x.ReviewTag);

            q = ApplyApprovedListingOrder(q);

            return await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> CountApprovedForEntryAsync(int directoryEntryId, CancellationToken ct)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved)
                .CountAsync(ct);
        }

        public async Task<double?> AverageRatingForEntryApprovedAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.Rating.HasValue)
                .Select(r => (double)r.Rating!.Value);

            if (!await q.AnyAsync(ct)) return null;

            return await q.AverageAsync(ct);
        }

        // ✅ Global rating distribution counts (for your bars)
        public async Task<(int c1, int c2, int c3, int c4, int c5)> GetApprovedRatingCountsForEntryAsync(
            int directoryEntryId,
            CancellationToken ct)
        {
            var grouped = await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.Rating.HasValue
                         && r.Rating.Value >= 1
                         && r.Rating.Value <= 5)
                .GroupBy(r => r.Rating!.Value)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int Get(int rating) => grouped.FirstOrDefault(x => x.Rating == rating)?.Count ?? 0;

            return (Get(1), Get(2), Get(3), Get(4), Get(5));
        }

        // =========================================================
        // ✅ CORRECT PAGE MAP (NO RAW SQL)
        //
        // Returns: ReviewId -> PageNumber
        // Page is computed using EXACT same ordering as listing:
        // CreateDate DESC, ReviewId DESC
        //
        // Position formula for descending order:
        // pos = COUNT(approved where date > r.date OR (date== AND id>=))
        // page = ceil(pos / pageSize)
        // =========================================================
        public async Task<Dictionary<int, int>> GetApprovedReviewPageMapAsync(
            IEnumerable<int> reviewIds,
            int pageSize,
            CancellationToken ct = default)
        {
            var ids = (reviewIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return new Dictionary<int, int>();
            if (pageSize < 1) pageSize = 10;

            var rows = await this.Set.AsNoTracking()
                .Where(r => ids.Contains(r.DirectoryEntryReviewId)
                         && r.ModerationStatus == ReviewModerationStatus.Approved)
                .Select(r => new
                {
                    r.DirectoryEntryReviewId,
                    Position = this.Set.AsNoTracking()
                        .Where(x => x.DirectoryEntryId == r.DirectoryEntryId
                                 && x.ModerationStatus == ReviewModerationStatus.Approved
                                 && (
                                     x.CreateDate > r.CreateDate
                                     || (x.CreateDate == r.CreateDate && x.DirectoryEntryReviewId >= r.DirectoryEntryReviewId)
                                 ))
                        .Count()
                })
                .ToListAsync(ct);

            var result = new Dictionary<int, int>(rows.Count);

            foreach (var row in rows)
            {
                int page = (int)Math.Ceiling(row.Position / (double)pageSize);
                if (page < 1) page = 1;
                result[row.DirectoryEntryReviewId] = page;
            }

            return result;
        }

        // ---------------------------
        // Moderation queue helpers
        // ---------------------------
        public async Task<List<DirectoryEntryReview>> ListByStatusAsync(
            ReviewModerationStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == status)
                .OrderBy(r => r.CreateDate)
                .ThenBy(r => r.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(ReviewModerationStatus status, CancellationToken ct = default) =>
            this.Set.Where(r => r.ModerationStatus == status).CountAsync(ct);

        public async Task SetModerationStatusAsync(
            int id, ReviewModerationStatus status, string reason, CancellationToken ct = default)
        {
            var review = await this.Set.FindAsync(new object[] { id }, ct);
            if (review is null) return;

            review.RejectionReason = reason;
            review.ModerationStatus = status;
            review.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        public Task ApproveAsync(int id, CancellationToken ct = default) =>
            this.SetModerationStatusAsync(id, ReviewModerationStatus.Approved, string.Empty, ct);

        public Task RejectAsync(int id, string reason, CancellationToken ct = default) =>
            this.SetModerationStatusAsync(id, ReviewModerationStatus.Rejected, reason, ct);

        // ---------------------------
        // Latest approved (homepage)
        // ---------------------------
        public async Task<List<DirectoryEntryReview>> ListLatestApprovedAsync(int count = 10, CancellationToken ct = default)
        {
            if (count < 1) count = 10;

            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Include(r => r.DirectoryEntry)
                    .ThenInclude(de => de.SubCategory!)
                        .ThenInclude(sc => sc.Category!)
                .Include(r => r.ReviewTags)
                    .ThenInclude(rt => rt.ReviewTag)
                .OrderByDescending(r => r.UpdateDate ?? r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId)
                .Take(count)
                .ToListAsync(ct);
        }

        // ---------------------------
        // Other helpers you already had
        // ---------------------------
        public async Task<List<DirectoryEntryReview>> ListForEntryAsync(
            int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId)
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<double?> AverageRatingForEntryAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId && r.Rating.HasValue)
                .Select(r => (double)r.Rating!.Value);

            if (!await q.AnyAsync(ct)) return null;

            return await q.AverageAsync(ct);
        }

        public async Task<Dictionary<int, RatingSummaryDto>> GetRatingSummariesAsync(IReadOnlyCollection<int> directoryEntryIds)
        {
            if (directoryEntryIds == null || directoryEntryIds.Count == 0)
                return new Dictionary<int, RatingSummaryDto>();

            var rows = await this.Set.AsNoTracking()
                .Where(r => directoryEntryIds.Contains(r.DirectoryEntryId)
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.Rating.HasValue)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new RatingSummaryDto
                {
                    DirectoryEntryId = g.Key,
                    ReviewCount = g.Count(),
                    AvgRating = g.Average(x => (double)x.Rating!.Value)
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(x => x.DirectoryEntryId, x => x);
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

        public async Task<Dictionary<int, ApprovedReviewStatsRow>> GetApprovedReviewStatsByEntryAsync(CancellationToken ct = default)
        {
            var rows = await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new ApprovedReviewStatsRow
                {
                    DirectoryEntryId = g.Key,
                    Count = g.Count(),
                    Last = g.Max(x => (x.UpdateDate ?? x.CreateDate))
                })
                .ToListAsync(ct);

            return rows.ToDictionary(x => x.DirectoryEntryId, x => x);
        }

        public async Task<Dictionary<int, DateTime>> GetApprovedReviewLastModifiedByEntryAsync(CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new
                {
                    EntryId = g.Key,
                    Last = g.Max(x => x.UpdateDate ?? x.CreateDate)
                })
                .ToDictionaryAsync(x => x.EntryId, x => x.Last, ct);
        }

        public async Task<Dictionary<int, int>> GetApprovedReviewCountsByEntryAsync(CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new
                {
                    EntryId = g.Key,
                    Cnt = g.Count()
                })
                .ToDictionaryAsync(x => x.EntryId, x => x.Cnt, ct);
        }

        public async Task<DirectoryEntryReview?> GetWithTagsByIdAsync(int id, CancellationToken ct = default)
        {
            return await this.Set
                .Include(r => r.ReviewTags)
                    .ThenInclude(rt => rt.ReviewTag)
                .FirstOrDefaultAsync(r => r.DirectoryEntryReviewId == id, ct);
        }
    }
}
