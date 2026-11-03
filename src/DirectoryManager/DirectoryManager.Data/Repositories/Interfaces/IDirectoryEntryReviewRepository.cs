using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryReviewRepository
    {
        Task<DirectoryEntryReview?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<List<DirectoryEntryReview>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
        Task<int> CountAsync(CancellationToken ct = default);
        Task AddAsync(DirectoryEntryReview entity, CancellationToken ct = default);
        Task UpdateAsync(DirectoryEntryReview entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);

        // Optional helpers (leave if your model includes these fields; safe to remove if not)
        Task<List<DirectoryEntryReview>> ListForEntryAsync(int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default);
        Task<double?> AverageRatingForEntryAsync(int directoryEntryId, CancellationToken ct = default);

        IQueryable<DirectoryEntryReview> Query();

        Task<List<DirectoryEntryReview>> ListApprovedForEntryAsync(int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default);
        Task<double?> AverageRatingForEntryApprovedAsync(int directoryEntryId, CancellationToken ct = default);
        Task<List<DirectoryEntryReview>> ListByStatusAsync(ReviewModerationStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default);
        Task<int> CountByStatusAsync(ReviewModerationStatus status, CancellationToken ct = default);
        Task SetModerationStatusAsync(int id, ReviewModerationStatus status, string reason, CancellationToken ct = default);
        Task ApproveAsync(int id, CancellationToken ct = default);
        Task RejectAsync(int id, string reason, CancellationToken ct = default);
        Task<Dictionary<int, DateTime>> GetLatestApprovedReviewDatesByEntryAsync(CancellationToken ct = default);
        Task<List<DirectoryEntryReview>> ListLatestApprovedAsync(int count = 10, CancellationToken ct = default);

        /// <summary>
        /// Problems reported in approved reviews of one listing: review-tag name → number of reviews
        /// carrying it, for tags at Warning level or worse (e.g. "Funds held", "KYC Issue"). Tags that
        /// describe the review itself ("suspicious") are left out.
        /// </summary>
        Task<Dictionary<string, int>> GetIssueTagCountsAsync(int directoryEntryId);

        /// <summary>Amount band (MinUsd/MaxUsd) of each approved review of a listing that states an amount.</summary>
        Task<List<(decimal? Min, decimal? Max)>> GetVerifiedOrderAmountBandsAsync(int directoryEntryId, CancellationToken ct = default);

        Task<Dictionary<int, RatingSummaryDto>> GetRatingSummariesAsync(IReadOnlyCollection<int> directoryEntryIds);
        public Task<DirectoryEntryReview?> GetWithTagsByIdAsync(int id, CancellationToken ct = default);
        Task<int> CountApprovedForEntryAsync(int directoryEntryId, CancellationToken ct);

        // Count of approved, RATED reviews INCLUDING the official review — matches the
        // denominator of AverageRatingForEntryApprovedAsync so aggregate rating + count stay
        // consistent (e.g. for the sponsored-listing ad feed, where the official review counts).
        Task<int> CountApprovedRatedIncludingOfficialForEntryAsync(int directoryEntryId, CancellationToken ct = default);

        Task<List<DirectoryEntryReview>> ListOfficialForEntryAsync(int directoryEntryId, CancellationToken ct);
        Task<Dictionary<int, int>> GetApprovedReviewCountsByEntryAsync(CancellationToken ct = default);
        Task<Dictionary<int, DateTime>> GetApprovedReviewLastModifiedByEntryAsync(CancellationToken ct = default);
        Task<Dictionary<int, ApprovedReviewStatsRow>> GetApprovedReviewStatsByEntryAsync(CancellationToken ct = default);

        Task<(int c1, int c2, int c3, int c4, int c5)> GetApprovedRatingCountsForEntryAsync(int directoryEntryId, CancellationToken ct);

        Task<Dictionary<int, int>> GetApprovedReviewPageMapAsync(IEnumerable<int> reviewIds, int pageSize, CancellationToken ct = default);

        Task<List<DirectoryEntryReview>> ListByAuthorFingerprintAsync(
            string fingerprint,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<int> CountByAuthorFingerprintAsync(string fingerprint, CancellationToken ct = default);

        Task<bool> DeleteOwnedAsync(int reviewId, string fingerprint, CancellationToken ct = default);

        Task<Dictionary<string, int>> GetApprovedReviewCountsByAuthorAsync(CancellationToken ct = default);
        Task EnsureTagAsync(int directoryEntryReviewId, int reviewTagId, CancellationToken ct);
        Task<bool> ExistsByBodyAsync(string body, int? excludeReviewId = null, CancellationToken ct = default);
    }
}