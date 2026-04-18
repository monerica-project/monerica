using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntrySelection : UserStateInfo
    {
        [Key]
        public int DirectoryEntrySelectionId { get; set; }

        public int DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public EntrySelectionType EntrySelectionType { get; set; } = EntrySelectionType.None;
    }
}