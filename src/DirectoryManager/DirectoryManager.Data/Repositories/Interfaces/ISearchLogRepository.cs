using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISearchLogRepository
    {
        /// <summary>
        /// Inserts a new search record.
        /// </summary>
        Task CreateAsync(SearchLog log);
    }
}
