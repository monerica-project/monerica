using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IReviewTagRepository
    {
        IQueryable<ReviewTag> Query();
        Task<List<ReviewTag>> ListEnabledAsync(CancellationToken ct = default);
        Task<List<ReviewTag>> ListAllAsync(CancellationToken ct = default);
        Task<ReviewTag?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ReviewTag?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task AddAsync(ReviewTag tag, CancellationToken ct = default);
        Task UpdateAsync(ReviewTag tag, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
