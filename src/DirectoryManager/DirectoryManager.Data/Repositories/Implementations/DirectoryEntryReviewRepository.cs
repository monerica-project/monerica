using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
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
        // ✅ SINGLE SOURCE OF TRUTH (PUBLIC REVIEW ORDER)
        //
        // Newest first by CreateDate (NOT UpdateDate):
        // ORDER BY CreateDate DESC, ReviewId DESC
        //
        // This guarantees:
        // - "Newest created review" always appears first
        // - paging is stable (tie-breaker = ReviewId)
        // - moderation/edits won't reshuffle public order
        // =========================================================
        private static IOrderedQueryable<DirectoryEntryReview> ApplyApprovedListingOrder(IQueryable<DirectoryEntryReview> q)
        {
            return q
                .OrderByDescending(r => r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId);
        }

        private IQueryable<DirectoryEntryReview> ApprovedForEntryQuery(int directoryEntryId)
        {
            return this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && !r.IsOfficial);
        }

        // ---------------------------
        // CRUD / admin
        // ---------------------------
        public async Task<DirectoryEntryReview?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<List<DirectoryEntryReview>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
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
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewId)
                .Skip(PagingHelper.Skip(page, pageSize))
                .Take(PagingHelper.Take(pageSize))
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task AddAsync(DirectoryEntryReview entity, CancellationToken ct = default)
        {
            // Every review must carry an author thumbprint (PGP fingerprint). The public
            // review flow always supplies one from the PGP-verified session. If any path ever
            // tries to persist a review with no key, it can't be trusted or auto-published:
            // at this single insert choke point we label the missing key and force the review
            // into the moderation queue (Flagged) so a human reviews it, instead of silently
            // accepting a keyless, unattributable review.
            if (string.IsNullOrWhiteSpace(entity.AuthorFingerprint))
            {
                entity.AuthorFingerprint = "UNKNOWN";
                entity.ModerationStatus = ReviewModerationStatus.Flagged;
            }

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

        public async Task DeleteAsync(int id, CancellationToken ct = default, string deletedBy = DeletedReviewRecord.ByAdmin)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            // Keep an admin-only copy (review, tags, replies); it commits with the delete below.
            await DeletedReviewArchive.AddForReviewAsync(this.context, existing, deletedBy, ct);
            await this.FlattenRepliesAsync(id, ct);
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
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            IQueryable<DirectoryEntryReview> q = this.ApprovedForEntryQuery(directoryEntryId)
                .Include(r => r.ReviewTags)
                    .ThenInclude(x => x.ReviewTag);

            q = ApplyApprovedListingOrder(q);

            return await q
                .Skip(PagingHelper.Skip(page, pageSize))
                .Take(PagingHelper.Take(pageSize))
                .ToListAsync(ct);
        }

        public async Task<int> CountApprovedForEntryAsync(int directoryEntryId, CancellationToken ct)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && !r.IsOfficial)
                .CountAsync(ct);
        }

        // ✅ Pinned official reviews — always shown above the crowd, never paged,
        // excluded from the crowd count/average so they don't skew it.
        public async Task<List<DirectoryEntryReview>> ListOfficialForEntryAsync(
            int directoryEntryId,
            CancellationToken ct)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.IsOfficial)
                .Include(r => r.ReviewTags)
                    .ThenInclude(x => x.ReviewTag)
                .OrderByDescending(r => r.TestedAt ?? r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId)
                .ToListAsync(ct);
        }

        public async Task<double?> AverageRatingForEntryApprovedAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.Rating.HasValue)
                .Select(r => (double)r.Rating!.Value);

            if (!await q.AnyAsync(ct))
            {
                return null;
            }

            return await q.AverageAsync(ct);
        }

        // Count of approved RATED reviews INCLUDING the official one — the same set
        // AverageRatingForEntryApprovedAsync averages over, so rating and count agree.
        public async Task<int> CountApprovedRatedIncludingOfficialForEntryAsync(int directoryEntryId, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.Rating.HasValue)
                .CountAsync(ct);
        }

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

        public async Task<List<DirectoryEntryReview>> ListByAuthorFingerprintAsync(
            string fingerprint, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 25;
            }

            fingerprint = (fingerprint ?? string.Empty).Trim();

            return await this.Set.AsNoTracking()
                .Where(r => r.AuthorFingerprint == fingerprint
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Include(r => r.DirectoryEntry)
                    .ThenInclude(de => de.SubCategory!)
                        .ThenInclude(sc => sc.Category!)
                .OrderByDescending(r => r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId)
                .Skip(PagingHelper.Skip(page, pageSize))
                .Take(PagingHelper.Take(pageSize))
                .ToListAsync(ct);
        }

        public Task<bool> HasApprovedByAuthorFingerprintAsync(string? fingerprint, CancellationToken ct = default)
        {
            fingerprint = (fingerprint ?? string.Empty).Trim();
            if (fingerprint.Length == 0)
            {
                return Task.FromResult(false);
            }

            return this.Set.AsNoTracking()
                .AnyAsync(r => r.AuthorFingerprint == fingerprint && r.ModerationStatus == ReviewModerationStatus.Approved, ct);
        }

        public Task<int> CountByAuthorFingerprintAsync(string fingerprint, CancellationToken ct = default)
        {
            fingerprint = (fingerprint ?? string.Empty).Trim();

            return this.Set.AsNoTracking()
                .Where(r => r.AuthorFingerprint == fingerprint
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .CountAsync(ct);
        }

        public async Task<bool> DeleteOwnedAsync(int reviewId, string fingerprint, CancellationToken ct = default)
        {
            fingerprint = (fingerprint ?? string.Empty).Trim();

            // Load tracked entity so we can remove it
            var review = await this.Set
                .FirstOrDefaultAsync(r => r.DirectoryEntryReviewId == reviewId && r.AuthorFingerprint == fingerprint, ct);

            if (review is null)
            {
                return false;
            }

            await DeletedReviewArchive.AddForReviewAsync(this.context, review, DeletedReviewRecord.ByAuthor, ct);
            await this.FlattenRepliesAsync(review.DirectoryEntryReviewId, ct);
            this.Set.Remove(review);
            await this.context.SaveChangesAsync(ct);
            return true;
        }

        public async Task EnsureTagAsync(int directoryEntryReviewId, int reviewTagId, CancellationToken ct = default)
        {
            // ✅ Adjust DbSet name if yours differs
            var joins = this.context.DirectoryEntryReviewTags;

            var exists = await joins.AsNoTracking().AnyAsync(
                x => x.DirectoryEntryReviewId == directoryEntryReviewId &&
                     x.ReviewTagId == reviewTagId,
                ct);

            if (exists)
            {
                return;
            }

            joins.Add(new DirectoryEntryReviewTag
            {
                DirectoryEntryReviewId = directoryEntryReviewId,
                ReviewTagId = reviewTagId
            });

            await this.context.SaveChangesAsync(ct);
        }

        public async Task<Dictionary<string, int>> GetApprovedReviewCountsByAuthorAsync(CancellationToken ct = default)
        {
            var rows = await this.Set.AsNoTracking()
                .Where(r =>
                    r.ModerationStatus == ReviewModerationStatus.Approved
                    && !string.IsNullOrWhiteSpace(r.AuthorFingerprint)
                    && r.DirectoryEntry != null
                    && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .GroupBy(r => r.AuthorFingerprint.Trim())
                .Select(g => new { Fingerprint = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return rows.ToDictionary(
                x => x.Fingerprint,
                x => x.Count,
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<Dictionary<int, int>> GetApprovedReviewPageMapAsync(
            IEnumerable<int> reviewIds,
            int pageSize,
            CancellationToken ct = default)
        {
            var ids = (reviewIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            // IMPORTANT:
            // Must match ApplyApprovedListingOrder:
            // Order = CreateDate DESC, ReviewId DESC
            //
            // Position is 1-based count of items that come "before or equal" this review
            // in that ordering (so we can compute page = ceil(position / pageSize)).
            // Official reviews are pinned above the crowd on page 1 and are not part of the paged
            // crowd list (ApprovedForEntryQuery excludes them), so they map to page 1 and are not
            // counted in a crowd review's position.
            var rows = await this.Set.AsNoTracking()
                .Where(r => ids.Contains(r.DirectoryEntryReviewId)
                         && r.ModerationStatus == ReviewModerationStatus.Approved)
                .Select(r => new
                {
                    r.DirectoryEntryReviewId,
                    Position = r.IsOfficial ? 1 : this.Set.AsNoTracking()
                        .Where(x => x.DirectoryEntryId == r.DirectoryEntryId
                                 && x.ModerationStatus == ReviewModerationStatus.Approved
                                 && !x.IsOfficial
                                 && (

                                     // x comes before r if it has a later CreateDate
                                     x.CreateDate > r.CreateDate

                                     // or same CreateDate but higher/equal ReviewId (DESC id)
                                     || (x.CreateDate == r.CreateDate
                                         && x.DirectoryEntryReviewId >= r.DirectoryEntryReviewId)))
                        .Count()
                })
                .ToListAsync(ct);

            var result = new Dictionary<int, int>(rows.Count);

            foreach (var row in rows)
            {
                int page = (int)Math.Ceiling(row.Position / (double)pageSize);
                if (page < 1)
                {
                    page = 1;
                }

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
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == status)
                .OrderBy(r => r.CreateDate)
                .ThenBy(r => r.DirectoryEntryReviewId)
                .Skip(PagingHelper.Skip(page, pageSize))
                .Take(PagingHelper.Take(pageSize))
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(ReviewModerationStatus status, CancellationToken ct = default) =>
            this.Set.Where(r => r.ModerationStatus == status).CountAsync(ct);

        public async Task SetModerationStatusAsync(
            int id, ReviewModerationStatus status, string reason, CancellationToken ct = default)
        {
            var review = await this.Set.FindAsync([id], ct);
            if (review is null)
            {
                return;
            }

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
            if (count < 1)
            {
                count = 10;
            }

            return await this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Include(r => r.DirectoryEntry)
                    .ThenInclude(de => de.SubCategory!)
                        .ThenInclude(sc => sc.Category!)
                .Include(r => r.ReviewTags)
                    .ThenInclude(rt => rt.ReviewTag)
                .OrderByDescending(r => r.CreateDate)
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
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            return await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId)
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.DirectoryEntryReviewId)
                .Skip(PagingHelper.Skip(page, pageSize))
                .Take(PagingHelper.Take(pageSize))
                .ToListAsync(ct);
        }

        public async Task<double?> AverageRatingForEntryAsync(int directoryEntryId, CancellationToken ct = default)
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

        public async Task<Dictionary<string, int>> GetIssueTagCountsAsync(int directoryEntryId)
        {
            var rows = await this.context.DirectoryEntryReviewTags.AsNoTracking()
                .Where(t => t.DirectoryEntryReview.DirectoryEntryId == directoryEntryId
                         && t.DirectoryEntryReview.ModerationStatus == ReviewModerationStatus.Approved
                         && t.ReviewTag.Level >= ReviewTagLevel.Warning
                         && t.ReviewTag.Slug != ReviewTagSlugs.Suspicious)
                .GroupBy(t => t.ReviewTag.Name)
                .Select(g => new { Name = g.Key, Count = g.Select(x => x.DirectoryEntryReviewId).Distinct().Count() })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(x => x.Name, x => x.Count);
        }

        public async Task<List<(decimal? Min, decimal? Max)>> GetVerifiedOrderAmountBandsAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var rows = await this.context.DirectoryEntryReviewTags.AsNoTracking()
                .Where(t => t.DirectoryEntryReview.DirectoryEntryId == directoryEntryId
                         && t.DirectoryEntryReview.ModerationStatus == ReviewModerationStatus.Approved
                         && (t.ReviewTag.MinUsd != null || t.ReviewTag.MaxUsd != null))
                .Select(t => new { t.DirectoryEntryReviewId, t.ReviewTag.MinUsd, t.ReviewTag.MaxUsd })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // One band per review (a review should only carry one).
            return rows.GroupBy(r => r.DirectoryEntryReviewId)
                .Select(g => (g.First().MinUsd, g.First().MaxUsd))
                .ToList();
        }

        public async Task<Dictionary<int, RatingSummaryDto>> GetRatingSummariesAsync(IReadOnlyCollection<int> directoryEntryIds)
        {
            if (directoryEntryIds == null || directoryEntryIds.Count == 0)
            {
                return new Dictionary<int, RatingSummaryDto>();
            }

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

            var result = rows.ToDictionary(x => x.DirectoryEntryId, x => x);

            // Verified orders: approved reviews carrying the "Valid Order" tag.
            var verified = await this.Set.AsNoTracking()
                .Where(r => directoryEntryIds.Contains(r.DirectoryEntryId)
                         && r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.ReviewTags.Any(t => t.ReviewTag.Slug == ReviewTagSlugs.ValidOrder))
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var v in verified)
            {
                if (!result.TryGetValue(v.Id, out var dto))
                {
                    dto = new RatingSummaryDto { DirectoryEntryId = v.Id };
                    result[v.Id] = dto;
                }

                dto.VerifiedOrderCount = v.Count;
            }

            return result;
        }

        // NOTE:
        // These "latest/last modified" methods were originally based on UpdateDate ?? CreateDate.
        // Leaving them as-is preserves "last modified" semantics for caching/sitemap/etc.
        // Public display order no longer depends on UpdateDate.
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
                    PagedCount = g.Count(x => !x.IsOfficial),
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
                .ToDictionaryAsync(x => x.EntryId, x => x.Cnt);
        }

        public async Task<DirectoryEntryReview?> GetWithTagsByIdAsync(int id, CancellationToken ct = default)
        {
            return await this.Set
                .Include(r => r.ReviewTags)
                    .ThenInclude(rt => rt.ReviewTag)
                .FirstOrDefaultAsync(r => r.DirectoryEntryReviewId == id, ct);
        }

        public async Task<bool> ExistsByBodyAsync(string body, int? excludeReviewId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            var normalized = body.Trim();

            var q = this.Set.AsNoTracking()
                .Where(r => r.Body != null && r.Body.Trim() == normalized);

            if (excludeReviewId.HasValue)
            {
                q = q.Where(r => r.DirectoryEntryReviewId != excludeReviewId.Value);
            }

            return await q.AnyAsync(ct);
        }

        // ---------------------------
        // Public cross-listing pages (/reviews, /incidents)
        // ---------------------------
        public async Task<PagedResult<DirectoryEntryReview>> ListPublicAsync(PublicReviewFilter filter, CancellationToken ct = default)
        {
            filter ??= new PublicReviewFilter();

            // Approved reviews on listings that are still shown (Removed listings have no page).
            IQueryable<DirectoryEntryReview> q = this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed);

            if (filter.CategoryId is int categoryId)
            {
                q = q.Where(r => r.DirectoryEntry.SubCategory != null && r.DirectoryEntry.SubCategory.CategoryId == categoryId);
            }

            if (filter.Rating is byte rating)
            {
                q = q.Where(r => r.Rating == rating);
            }

            if (filter.TagId is int tagId)
            {
                q = q.Where(r => r.ReviewTags.Any(t => t.ReviewTagId == tagId && t.ReviewTag.IsEnabled));
            }

            if (filter.WithReplies)
            {
                q = q.Where(r => r.Comments.Any(c => c.ModerationStatus == ReviewModerationStatus.Approved));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                // Parameterized ILIKE with the user's % _ \ escaped (Postgres' default escape is \).
                var like = $"%{EscapeLike(filter.Search)}%";
                q = q.Where(r =>
                    EF.Functions.ILike(r.Body, like)
                    || (r.Title != null && EF.Functions.ILike(r.Title, like))
                    || EF.Functions.ILike(r.DirectoryEntry.Name, like));
            }

            int total = await q.CountAsync(ct);
            int pageSize = PublicReviewFilter.PageSize;
            int page = Math.Clamp(filter.Page, 1, PagingHelper.TotalPages(total, pageSize));

            IOrderedQueryable<DirectoryEntryReview> ordered = filter.Sort switch
            {
                PublicReviewSort.Oldest => q.OrderBy(r => r.CreateDate).ThenBy(r => r.DirectoryEntryReviewId),
                PublicReviewSort.Highest => q.OrderBy(r => r.Rating == null).ThenByDescending(r => r.Rating)
                    .ThenByDescending(r => r.CreateDate).ThenByDescending(r => r.DirectoryEntryReviewId),
                PublicReviewSort.Lowest => q.OrderBy(r => r.Rating == null).ThenBy(r => r.Rating)
                    .ThenByDescending(r => r.CreateDate).ThenByDescending(r => r.DirectoryEntryReviewId),
                _ => q.OrderByDescending(r => r.CreateDate).ThenByDescending(r => r.DirectoryEntryReviewId),
            };

            var items = total == 0
                ? new List<DirectoryEntryReview>()
                : await ordered
                    .Skip(PagingHelper.Skip(page, pageSize))
                    .Take(pageSize)
                    .Include(r => r.DirectoryEntry)
                        .ThenInclude(e => e.SubCategory!)
                            .ThenInclude(sc => sc.Category!)
                    .Include(r => r.ReviewTags)
                        .ThenInclude(rt => rt.ReviewTag)
                    .AsSplitQuery()
                    .ToListAsync(ct);

            return new PagedResult<DirectoryEntryReview> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<PagedResult<DirectoryEntryReview>> ListProblemReportsAsync(
            DateTime? sinceUtc,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            pageSize = PagingHelper.ClampPageSize(pageSize, 20);

            // Same conditions as ProblemTagRule.IsProblemTag, in SQL.
            var minLevel = ProblemTagRule.MinimumLevel;
            IQueryable<DirectoryEntryReview> q = this.Set.AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                         && r.DirectoryEntry != null
                         && r.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed
                         && r.ReviewTags.Any(t =>
                                t.ReviewTag.IsEnabled
                                && t.ReviewTag.ReviewerSelectable
                                && t.ReviewTag.Level >= minLevel
                                && t.ReviewTag.MinUsd == null
                                && t.ReviewTag.MaxUsd == null
                                && t.ReviewTag.Slug != ReviewTagSlugs.Suspicious));

            if (sinceUtc.HasValue)
            {
                var since = sinceUtc.Value;
                q = q.Where(r => r.CreateDate >= since);
            }

            int total = await q.CountAsync(ct);
            page = Math.Clamp(page, 1, PagingHelper.TotalPages(total, pageSize));

            var items = total == 0
                ? new List<DirectoryEntryReview>()
                : await q
                    .OrderByDescending(r => r.CreateDate)
                    .ThenByDescending(r => r.DirectoryEntryReviewId)
                    .Skip(PagingHelper.Skip(page, pageSize))
                    .Take(pageSize)
                    .Include(r => r.DirectoryEntry)
                    .Include(r => r.ReviewTags)
                        .ThenInclude(rt => rt.ReviewTag)
                    .AsSplitQuery()
                    .ToListAsync(ct);

            return new PagedResult<DirectoryEntryReview> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public async Task<Dictionary<int, int>> GetApprovedReplyCountsAsync(IEnumerable<int> reviewIds, CancellationToken ct = default)
        {
            var ids = (reviewIds ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            return await this.context.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => ids.Contains(c.DirectoryEntryReviewId) && c.ModerationStatus == ReviewModerationStatus.Approved)
                .GroupBy(c => c.DirectoryEntryReviewId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        }

        private static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        // Replies on a review cascade-delete with it, but ParentCommentId is a RESTRICT self-FK,
        // so a nested reply chain would block that cascade. Flatten the thread first (all replies
        // become top-level) so the review and its replies can then be removed cleanly.
        private async Task FlattenRepliesAsync(int reviewId, CancellationToken ct)
        {
            var nested = await this.context.DirectoryEntryReviewComments
                .Where(c => c.DirectoryEntryReviewId == reviewId && c.ParentCommentId != null)
                .ToListAsync(ct);

            if (nested.Count == 0)
            {
                return;
            }

            foreach (var c in nested)
            {
                c.ParentCommentId = null;
                c.ParentComment = null;
            }

            await this.context.SaveChangesAsync(ct);
        }
    }
}