using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISearchLogRepository
    {
        Task CreateAsync(SearchLog log);

        /// <summary>
        /// Gets a report of all distinct search terms between <paramref name="start"/> (inclusive)
        /// and <paramref name="end"/> (exclusive), with counts, first/last, and percentage of total.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of report items.</returns>
        Task<IReadOnlyList<SearchReportItem>> GetReportAsync(DateTime start, DateTime end);
    }
}