using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IRaffleRepository
    {
        Task<Raffle?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Returns the current "active" raffle (enabled and within [StartDate, EndDate]).
        /// If multiple qualify, returns the one with the most recent StartDate.
        /// </summary>
        Task<Raffle?> GetActiveAsync(DateTime utcNow, CancellationToken ct = default);

        Task<List<Raffle>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

        Task<int> CountAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns a list of raffles with aggregated entry counts, for the admin index page.
        /// </summary>
        Task<List<RaffleSummaryDto>> ListWithCountsAsync(
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default);

        Task AddAsync(Raffle entity, CancellationToken ct = default);
        Task UpdateAsync(Raffle entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
