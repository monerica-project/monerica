using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryEditViewModel
    {
        public int DirectoryEntryId { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        [Required]
        public int SubCategoryId { get; set; }

        [Required]
        [MaxLength(65)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        public string? LinkA { get; set; }
        public string? Link2 { get; set; }
        public string? Link2A { get; set; }
        public string? Link3 { get; set; }
        public string? Link3A { get; set; }
        public string? ProofLink { get; set; }
        public string? VideoLink { get; set; }
        public string? Location { get; set; }
        public string? CountryCode { get; set; }
        public string? Processor { get; set; }
        public string? Contact { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public string? PgpKey { get; set; }

        // ✅ Existing tags chosen via checkboxes (these are what get persisted)
        public List<int> SelectedTagIds { get; set; } = new ();

        // ✅ Optional: allow creating new tags by typing (also persisted if you want)
        // If you want “create/edit ONLY with checkboxes”, just remove this field & UI.
        [MaxLength(200)]
        public string? NewTagsCsv { get; set; }
        public string DirectoryEntryKey { get; set; } = string.Empty;
        public List<string> AdditionalLinks { get; set; } = new ();
    }
}
