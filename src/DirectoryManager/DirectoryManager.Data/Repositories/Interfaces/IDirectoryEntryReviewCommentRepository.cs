using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryReviewCommentRepository
    {
        IQueryable<DirectoryEntryReviewComment> Query();

        Task<DirectoryEntryReviewComment?> GetByIdAsync(
            int id, CancellationToken ct = default);

        Task AddAsync(
            DirectoryEntryReviewComment entity, CancellationToken ct = default);

        Task UpdateAsync(
            DirectoryEntryReviewComment entity, CancellationToken ct = default);

        Task DeleteAsync(
            int id, CancellationToken ct = default);

        // ---------------------------
        // Global list + counts (needed for moderation dashboard)
        // ---------------------------
        Task<List<DirectoryEntryReviewComment>> ListAsync(
            int page = 1, int pageSize = 50, CancellationToken ct = default);

        Task<int> CountAsync(CancellationToken ct = default);

        // ---------------------------
        // Listing helpers
        // ---------------------------
        Task<List<DirectoryEntryReviewComment>> ListApprovedForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default);

        Task<List<DirectoryEntryReviewComment>> ListForReviewAsync(
            int directoryEntryReviewId, CancellationToken ct = default);

        Task<List<DirectoryEntryReviewComment>> ListByStatusAsync(
            ReviewModerationStatus status,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default);

        Task<int> CountByStatusAsync(
            ReviewModerationStatus status, CancellationToken ct = default);

        // ---------------------------
        // Moderation helpers
        // ---------------------------
        Task SetModerationStatusAsync(
            int id, ReviewModerationStatus status, string reason, CancellationToken ct = default);

        Task ApproveAsync(
            int id, CancellationToken ct = default);

        Task RejectAsync(
            int id, string reason, CancellationToken ct = default);
    }
}