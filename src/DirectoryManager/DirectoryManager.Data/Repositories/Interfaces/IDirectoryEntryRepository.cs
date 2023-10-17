using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryRepository
    {
        Task<DirectoryEntry?> GetByIdAsync(int id);
        Task<DirectoryEntry?> GetByLinkAsync(string link);
        Task<IEnumerable<DirectoryEntry>> GetAllAsync();
        Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllowableEntries();
        Task CreateAsync(DirectoryEntry entry);
        Task UpdateAsync(DirectoryEntry entry);
        Task DeleteAsync(int id);
        public DateTime GetLastRevisionDate();
        public Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count);
        public Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays);
        public Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int pageSize, int pageNumber);
        public Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count);
        public Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int subCategoryId);
        public Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync();
        public Task<int> TotalActive();
    }
}