using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntriesAudit : UserStateInfo
    {
        [Key]
        public int DirectoryEntriesAuditId { get; set; }

        public int DirectoryEntryId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

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

        public int? SubCategoryId { get; set; }

        public virtual Subcategory? SubCategory { get; set; }

        [NotMapped]
        public string SubCategoryName { get; set; } = string.Empty;
    }
}