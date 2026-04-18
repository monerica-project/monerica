// Data/Repositories/Interfaces/ISearchBlacklistRepository.cs
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISearchBlacklistRepository
    {
        Task CreateAsync(string term);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(string term);

        // for cache lookup
        Task<IReadOnlyList<string>> GetAllTermsAsync();

        // for admin UI
        Task<int> CountAsync();
        Task<List<SearchBlacklistTerm>> ListPageAsync(int page, int pageSize);
    }
}
