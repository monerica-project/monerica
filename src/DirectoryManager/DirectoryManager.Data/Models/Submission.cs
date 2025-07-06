using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class Submission : StateInfo
    {
        [Key] // Primary Key
        public int SubmissionId { get; set; }

        [Display(Name = "Submission Status")]
        [Required]
        public SubmissionStatus SubmissionStatus { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link3 { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [MaxLength(1000)]
        public string? NoteToAdmin { get; set; }

        [MaxLength(75)]
        public string? Contact { get; set; }

        public int? SubCategoryId { get; set; }

        public Subcategory? SubCategory { get; set; }

        [MaxLength(255)]
        public string? SuggestedSubCategory { get; set; }

        [MaxLength(255)]
        public string? IpAddress { get; set; }

        public int? DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public DirectoryStatus? DirectoryStatus { get; set; }

        [MaxLength(255)]
        public string? Tags { get; set; }
    }
}