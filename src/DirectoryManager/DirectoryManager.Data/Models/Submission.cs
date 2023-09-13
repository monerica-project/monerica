using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models
{
    public class Submission : StateInfo
    {
        [Key] // Primary Key
        public int Id { get; set; }

        [Required]
        public SubmissionStatus SubmissionStatus { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        [Required]
        [MaxLength(500)]
        public string Link { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string? Link2 { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [MaxLength(75)]
        public string? Contact { get; set; }

        public int? SubCategoryId { get; set; }

        public SubCategory? SubCategory { get; set; }

        [MaxLength(255)]
        public string? SuggestedSubCategory { get; set; }

        [MaxLength(255)]
        public string? IpAddress { get; set; }

        public int? DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public DirectoryStatus? DirectoryStatus { get; set; }
    }
}