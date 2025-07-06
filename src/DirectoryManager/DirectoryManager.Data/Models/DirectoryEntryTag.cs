using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntryTag : StateInfo
    {
        public int DirectoryEntryId { get; set; }
        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        public int TagId { get; set; }
        public Tag Tag { get; set; } = null!;
    }
}
