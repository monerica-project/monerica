using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryRepository
    {
        Task<DirectoryEntry?> GetByIdAsync(int directoryEntryId);
        Task<DirectoryEntry?> GetBySubCategoryAndKeyAsync(int subcategorydId, string directoryEntryKey);
        Task<DirectoryEntry?> GetByLinkAsync(string link);
        Task<DirectoryEntry?> GetByNameAsync(string name);
        Task<DirectoryEntry?> GetByNameAndSubcategoryAsync(string name, int subcategoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllAsync();
        Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllowableAdvertisers();
        Task<IEnumerable<DirectoryEntry>> GetAllActiveEntries();
        Task CreateAsync(DirectoryEntry entry);
        Task UpdateAsync(DirectoryEntry entry);
        Task DeleteAsync(int directoryEntryId);
        DateTime GetLastRevisionDate();
        Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count);
        Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays);
        Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int pageSize, int pageNumber);
        Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count);
        Task<IEnumerable<DirectoryEntry>> GetActiveEntriesBySubcategoryAsync(int subCategoryId);
        Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int categoryId);
        Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync();
        Task<int> TotalActive();
        Task<Dictionary<int, DateTime>> GetLastModifiedDatesBySubCategoryAsync();
        Task<WeeklyDirectoryEntries> GetEntriesCreatedForPreviousWeekWithWeekKeyAsync();
        Task<MonthlyDirectoryEntries> GetEntriesCreatedForPreviousMonthWithMonthKeyAsync();
        Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByStatusAsync(DirectoryStatus status);
        Task<PagedResult<DirectoryEntry>> SearchAsync(
            string term,
            int page,
            int pageSize);

        Task<PagedResult<DirectoryEntry>> ListEntriesByCategoryAsync(int categoryId, int page, int pageSize);

        Task<PagedResult<DirectoryEntry>> GetActiveEntriesBySubcategoryPagedAsync(
            int subCategoryId,
            int page,
            int pageSize);

        Task<IReadOnlyList<DirectoryEntryUrl>> GetAllIdsAndUrlsAsync();
        Task<int> CountByCategoryAsync(int categoryId);
        Task<int> CountBySubcategoryAsync(int subCategoryId);
        Task<Dictionary<int, int>> GetCategoryEntryCountsAsync();
        Task<Dictionary<int, int>> GetSubcategoryEntryCountsAsync();
        Task<PagedResult<CountryWithCount>> ListActiveCountriesWithCountsPagedAsync(int page, int pageSize);
        Task<PagedResult<DirectoryEntry>> ListActiveEntriesByCountryPagedAsync(string countryCode, int page, int pageSize);
        Task<DirectoryEntry?> GetByKey(string directoryEntryKey);

        Task<PagedResult<DirectoryEntry>> FilterAsync(DirectoryFilterQuery q);

        Task<List<IdNameOption>> ListCategoryOptionsAsync();
        Task<List<IdNameOption>> ListSubCategoryOptionsAsync(int categoryId);
    }
}