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
        public DirectoryStatus DirectoryStatus { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; }

        [Required]
        [MaxLength(500)]
        public string Link2 { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        [MaxLength(255)]
        public string Location { get; set; }

        [Required]
        [MaxLength(255)]
        public string Processor { get; set; }

        [MaxLength(1000)]
        public string Note { get; set; }

        [MaxLength(75)]
        public string Contact { get; set; }

        public Category? Category { get; set; }

        [MaxLength(255)]
        public string SuggestedCategory { get; set; }

        public SubCategory? SubCategory { get; set; }

        [MaxLength(255)]
        public string SuggestedSubCategory { get; set; }

        [Required]
        [MaxLength(255)]
        public string? IpAddress { get; set; }
    }
}