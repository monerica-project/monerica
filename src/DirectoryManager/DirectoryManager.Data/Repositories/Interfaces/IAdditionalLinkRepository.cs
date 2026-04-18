using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAdditionalLinkRepository
    {
        Task<List<AdditionalLink>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);

        Task<AdditionalLink> CreateAsync(AdditionalLink model, CancellationToken ct = default);

        Task CreateManyAsync(IEnumerable<AdditionalLink> models, CancellationToken ct = default);

        Task DeleteAsync(int additionalLinkId, CancellationToken ct = default);

        Task DeleteByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);
    }
}