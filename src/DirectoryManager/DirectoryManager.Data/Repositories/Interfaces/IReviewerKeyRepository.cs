// DirectoryManager.Data/Repositories/Interfaces/IReviewerKeyRepository.cs
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IReviewerKeyRepository
    {
        Task<ReviewerKey?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ReviewerKey?> GetByFingerprintAsync(string fingerprint, CancellationToken ct = default);
        Task<List<ReviewerKey>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
        Task<int> CountAsync(CancellationToken ct = default);
        Task AddAsync(ReviewerKey entity, CancellationToken ct = default);
        Task UpdateAsync(ReviewerKey entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        IQueryable<ReviewerKey> Query(); // for advanced filtering
    }
}
