using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISiteCheckStatusRepository
    {
        Task<SiteCheckStatus?> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);

        /// <summary>Inserts a new row or updates the existing row for the entry.</summary>
        Task UpsertAsync(SiteCheckStatus model, CancellationToken ct = default);
    }
}
