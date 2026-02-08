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
        Task <Dictionary<int, DateTime>> GetLatestApprovedReviewDatesByEntryAsync(CancellationToken ct = default);
        Task<List<DirectoryEntryReview>> ListLatestApprovedAsync(int count = 10, CancellationToken ct = default);

        Task<Dictionary<int, RatingSummaryDto>> GetRatingSummariesAsync(IReadOnlyCollection<int> directoryEntryIds);
        public Task<DirectoryEntryReview?> GetWithTagsByIdAsync(int id, CancellationToken ct = default);
    }
}
