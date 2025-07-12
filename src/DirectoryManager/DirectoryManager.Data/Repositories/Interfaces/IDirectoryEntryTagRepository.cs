using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    /// <summary>
    /// Manages the many‐to‐many relationship between DirectoryEntry and Tag.
    /// </summary>
    public interface IDirectoryEntryTagRepository
    {
        /// <summary>
        /// Link a tag to an entry (no‐op if already linked).
        /// </summary>
        Task AssignTagAsync(int entryId, int tagId);

        /// <summary>
        /// Unlink a tag from an entry (no‐op if not linked).
        /// </summary>
        Task RemoveTagAsync(int entryId, int tagId);

        /// <summary>
        /// List all Tags assigned to the given entry.
        /// </summary>
        Task<IReadOnlyList<Tag>> GetTagsForEntryAsync(int entryId);

        /// <summary>
        /// List all DirectoryEntries that have the given tag name.
        /// </summary>
        Task<IReadOnlyList<DirectoryEntry>> ListEntriesForTagAsync(string tagName);
        Task<PagedResult<DirectoryEntry>> ListEntriesForTagPagedAsync(string tagName, int page, int pageSize);
    }
}
