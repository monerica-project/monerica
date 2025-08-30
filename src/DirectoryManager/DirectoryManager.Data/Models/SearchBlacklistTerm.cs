// Data/Models/SearchBlacklistTerm.cs
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// Case-insensitive blocked search term (we store the original, compare normalized).
    /// </summary>
    public class SearchBlacklistTerm : CreatedStateInfo
    {
        public int Id { get; set; }

        public string Term { get; set; } = null!;
    }
}
