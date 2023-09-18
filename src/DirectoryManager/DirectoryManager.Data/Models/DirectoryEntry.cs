using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntry : UserStateInfo, IEquatable<DirectoryEntry>
    {
        [Key] // Primary Key
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Name { get; set; }

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [MaxLength(75)]
        public string? Contact { get; set; }

        public virtual SubCategory? SubCategory { get; set; }

        public int? SubCategoryId { get; set; }

        public bool Equals(DirectoryEntry? other)
        {
            if (other == null) return false;

            return this.Id == other.Id; // Assuming Id is a unique identifier for DirectoryEntry
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}