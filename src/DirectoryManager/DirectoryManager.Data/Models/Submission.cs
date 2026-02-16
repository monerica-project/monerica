using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
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
        public string? ProofLink { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? VideoLink { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? PgpKey { get; set; }

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

        [MaxLength(2)]
        public string? CountryCode { get; set; }

        [MaxLength(2000)]
        public string? SelectedTagIdsCsv { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? RelatedLinksJson { get; set; }

        public DateOnly? FoundedDate { get; set; }

        [NotMapped]
        public List<string> RelatedLinks
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.RelatedLinksJson))
                {
                    return new List<string>();
                }

                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(this.RelatedLinksJson)
                               ?? new List<string>();

                    return list
                        .Select(x => (x ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                var normalized = (value ?? new List<string>())
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                this.RelatedLinksJson = normalized.Count == 0
                    ? null
                    : JsonSerializer.Serialize(normalized);
            }
        }
    }
}