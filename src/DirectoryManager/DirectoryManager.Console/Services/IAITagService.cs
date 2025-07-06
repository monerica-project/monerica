using System.Threading.Tasks;

namespace DirectoryManager.Console.Services
{
    /// <summary>
    /// Generates and persists AI-inferred tags for all DirectoryEntries.
    /// </summary>
    public interface IAITagService
    {
        /// <summary>
        /// For every DirectoryEntry in the database, calls the OpenAI Chat API
        /// to suggest up to 7 tags based on its Category, Subcategory, Name and Description,
        /// then saves them into the Tags & EntryTags tables.
        /// </summary>
        Task GenerateTagsForAllEntriesAsync();
    }
}
