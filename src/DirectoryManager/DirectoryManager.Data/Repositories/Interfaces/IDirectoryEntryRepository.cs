using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryRepository
    {
        Task<DirectoryEntry?> GetByIdAsync(int directoryEntryId);
        Task<DirectoryEntry?> GetBySubCategoryAndKeyAsync(int subcategorydId, string directoryEntryKey);
        Task<DirectoryEntry?> GetByLinkAsync(string link);
        Task<IEnumerable<DirectoryEntry>> GetAllAsync();
        Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllowableEntries();
        Task<IEnumerable<DirectoryEntry>> GetAllActiveEntries();
        Task CreateAsync(DirectoryEntry entry);
        Task UpdateAsync(DirectoryEntry entry);
        Task DeleteAsync(int directoryEntryId);
        DateTime GetLastRevisionDate();
        Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count);
        Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays);
        Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int pageSize, int pageNumber);
        Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count);
        Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int subCategoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync();
        Task<int> TotalActive();
        Task<Dictionary<int, DateTime>> GetLastModifiedDatesBySubCategoryAsync();
    }
}