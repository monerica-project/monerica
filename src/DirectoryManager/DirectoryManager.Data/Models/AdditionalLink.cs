using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public sealed class AdditionalLink : UserStateInfo, IEquatable<AdditionalLink>
    {
        [Key]
        public int AdditionalLinkId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        // Keeps display ordering stable (1..3). Optional but strongly recommended.
        [Range(1, 3)]
        public int SortOrder { get; set; } = 1;

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [ForeignKey(nameof(DirectoryEntryId))]
        public DirectoryEntry? DirectoryEntry { get; set; }

        public bool Equals(AdditionalLink? other)
        {
            if (other is null)
            {
                return false;
            }

            return this.AdditionalLinkId == other.AdditionalLinkId
                && this.DirectoryEntryId == other.DirectoryEntryId
                && this.SortOrder == other.SortOrder
                && this.Link == other.Link;
        }

        public override int GetHashCode() => this.AdditionalLinkId.GetHashCode();
    }
}