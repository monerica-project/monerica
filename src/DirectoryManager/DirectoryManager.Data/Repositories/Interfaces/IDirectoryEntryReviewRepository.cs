// DirectoryManager.Data/Repositories/Interfaces/IDirectoryEntryReviewRepository.cs
using DirectoryManager.Data.Models;

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
    }
}
