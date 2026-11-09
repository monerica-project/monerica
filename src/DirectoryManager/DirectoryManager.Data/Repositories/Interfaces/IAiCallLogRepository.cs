using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAiCallLogRepository
    {
        /// <summary>Newest first, optionally only one submission's calls or only failed calls.</summary>
        /// <returns>The page of rows and the total matching count.</returns>
        Task<(IReadOnlyList<AiCallLog> Items, int Total)> GetPageAsync(int page, int pageSize, int? submissionId, bool errorsOnly, CancellationToken ct = default);

        Task<AiCallLog?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Per-day totals (UTC) for the last <paramref name="days"/> days, newest first.</summary>
        /// <returns>One row per day that had calls.</returns>
        Task<IReadOnlyList<AiCallDailyTotal>> GetDailyTotalsAsync(int days, CancellationToken ct = default);
    }
}
