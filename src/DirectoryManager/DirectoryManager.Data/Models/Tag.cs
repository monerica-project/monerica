using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class Tag : StateInfo
    {
        public int TagId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = null!;

        public ICollection<DirectoryEntryTag> EntryTags { get; set; } = [];
    }
}