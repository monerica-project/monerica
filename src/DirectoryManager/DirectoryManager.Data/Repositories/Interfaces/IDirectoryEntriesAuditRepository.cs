using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntriesAuditRepository
    {
        Task<IEnumerable<DirectoryEntriesAudit>> GetAllAsync();
        Task CreateAsync(DirectoryEntriesAudit directoryEntriesAudit);
        Task<IEnumerable<DirectoryEntriesAudit>> GetAuditsForEntryAsync(int entryId);
    }
}