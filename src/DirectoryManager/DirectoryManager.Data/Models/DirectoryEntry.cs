using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntry : UserStateInfo, IEquatable<DirectoryEntry>
    {
        [Key] // Primary Key
        public int DirectoryEntryId { get; set; }

        [Required]
        [MaxLength(255)]
        required public string Name { get; set; }

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Affiliate link of main link.
        /// </summary>
        [MaxLength(500)]
        public string? LinkA { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        /// <summary>
        /// Affiliate link of second link.
        /// </summary>
        [MaxLength(500)]
        public string? Link2A { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        /// <summary>
        /// Affiliate link of third link.
        /// </summary>
        [MaxLength(500)]
        public string? Link3A { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        public DirectoryBadge DirectoryBadge { get; set; } = DirectoryBadge.Unknown;

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

        public virtual Subcategory? SubCategory { get; set; }

        public int SubCategoryId { get; set; }

        public bool Equals(DirectoryEntry? other)
        {
            if (other == null)
            {
                return false;
            }

            return this.DirectoryEntryId == other.DirectoryEntryId &&
                this.Name == other.Name &&
                this.Link == other.Link &&
                this.Link2 == other.Link2 &&
                this.DirectoryStatus == other.DirectoryStatus &&
                this.Description == other.Description &&
                this.Location == other.Location &&
                this.Processor == other.Processor &&
                this.Note == other.Note &&
                this.Contact == other.Contact &&
                this.SubCategoryId == other.SubCategoryId;
        }

        public override int GetHashCode()
        {
            return this.DirectoryEntryId.GetHashCode();
        }
    }
}