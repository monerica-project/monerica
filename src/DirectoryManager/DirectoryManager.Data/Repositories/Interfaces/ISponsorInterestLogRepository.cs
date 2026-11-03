using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsorInterestLogRepository
    {
        /// <summary>
        /// Logs a step unless the same listing / placement / step was already logged within
        /// <paramref name="dedupeWindow"/> (so page refreshes don't inflate the numbers).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task LogAsync(int directoryEntryId, SponsorshipType type, int? typeId, SponsorInterestStep step, TimeSpan dedupeWindow);

        /// <summary>Rows between start (inclusive) and end (exclusive).</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IReadOnlyList<SponsorInterestLog>> GetInRangeAsync(DateTime start, DateTime end);

        /// <summary>Listings with a paid sponsorship invoice created in the range → latest such date.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<Dictionary<int, DateTime>> GetPaidEntryIdsAsync(DateTime start, DateTime end);
    }
}
