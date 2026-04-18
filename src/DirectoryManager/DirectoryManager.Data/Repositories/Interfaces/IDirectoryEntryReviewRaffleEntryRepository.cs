using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryReviewRaffleEntryRepository
    {
        Task<DirectoryEntryReviewRaffleEntry?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Returns null if no entry exists for this review.</summary>
        Task<DirectoryEntryReviewRaffleEntry?> GetByReviewIdAsync(int directoryEntryReviewId, CancellationToken ct = default);

        /// <summary>
        /// Returns any entry for this fingerprint that is still "active"
        /// (Pending or Eligible). Returns null if the author has no live entry
        /// and may enter again (e.g. all prior entries are Ended/Disqualified/Paid).
        /// </summary>
        Task<DirectoryEntryReviewRaffleEntry?> GetActiveEntryByFingerprintAsync(string fingerprint, CancellationToken ct = default);

        Task<List<DirectoryEntryReviewRaffleEntry>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

        Task<List<DirectoryEntryReviewRaffleEntry>> ListByStatusAsync(RaffleEntryStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default);

        Task<int> CountAsync(CancellationToken ct = default);

        Task<int> CountByStatusAsync(RaffleEntryStatus status, CancellationToken ct = default);

        /// <summary>
        /// Adds a new raffle entry. Throws if a row already exists for the same review
        /// (unique constraint on DirectoryEntryReviewId).
        /// </summary>
        Task AddAsync(DirectoryEntryReviewRaffleEntry entity, CancellationToken ct = default);

        Task UpdateAsync(DirectoryEntryReviewRaffleEntry entity, CancellationToken ct = default);

        Task DeleteAsync(int id, CancellationToken ct = default);

        Task SetStatusAsync(int id, RaffleEntryStatus status, CancellationToken ct = default);
    }
}
