using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryGuaranteeRepository
    {
        Task<List<DirectoryEntryGuarantee>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);

        Task CreateManyAsync(IEnumerable<DirectoryEntryGuarantee> models, CancellationToken ct = default);

        Task DeleteByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);
    }
}
