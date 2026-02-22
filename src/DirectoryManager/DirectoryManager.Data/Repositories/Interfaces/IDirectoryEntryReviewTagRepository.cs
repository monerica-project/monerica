namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryReviewTagRepository
    {
        Task<List<int>> GetTagIdsForReviewAsync(int reviewId, CancellationToken ct = default);
        Task SetTagsForReviewAsync(int reviewId, IReadOnlyCollection<int> tagIds, string? userId, CancellationToken ct = default);
    }
}
