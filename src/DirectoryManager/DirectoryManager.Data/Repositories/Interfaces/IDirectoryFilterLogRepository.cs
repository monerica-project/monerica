using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryFilterLogRepository
    {
        Task CreateAsync(DirectoryFilterLog log);

        /// <summary>Raw filter-log rows between start (inclusive) and end (exclusive).</summary>
        Task<IReadOnlyList<DirectoryFilterLog>> GetInRangeAsync(DateTime start, DateTime end);
    }
}
