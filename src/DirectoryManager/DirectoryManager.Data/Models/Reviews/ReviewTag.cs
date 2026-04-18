using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    public class ReviewTag : UserStateInfo
    {
        [Key]
        public int ReviewTagId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty; // display

        [Required]
        [MaxLength(64)]
        public string Slug { get; set; } = string.Empty; // unique key for stable references

        [MaxLength(256)]
        public string? Description { get; set; }

        // ✅ New: controls badge “severity / color”
        public ReviewTagLevel Level { get; set; } = ReviewTagLevel.Neutral;

        public bool IsEnabled { get; set; } = true;

        public ICollection<DirectoryEntryReviewTag> ReviewLinks { get; set; } = new List<DirectoryEntryReviewTag>();
    }
}
