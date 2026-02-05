using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAdditionalLinkRepository
    {
        Task<IReadOnlyList<AdditionalLink>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct);

        /// <summary>
        /// Replaces the entry’s additional links with the provided set (max 3).
        /// Pass 0..3 links; blanks are ignored; duplicates are removed.
        /// </summary>
        Task<IReadOnlyList<AdditionalLink>> UpsertForDirectoryEntryAsync(
            int directoryEntryId,
            IEnumerable<string?> links,
            CancellationToken ct);

        Task DeleteForDirectoryEntryAsync(int directoryEntryId, CancellationToken ct);
    }
}