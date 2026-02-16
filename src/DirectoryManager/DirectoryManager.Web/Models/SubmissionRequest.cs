using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SubmissionRequest
    {
        public int? SubmissionId { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Link", Prompt = "https://yoursite.net")]
        public string Link { get; set; } = string.Empty;

        [Required]
        [MaxLength(65)]
        [Display(Name = "Name", Prompt = "Company or project name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(175)]
        [Display(Name = "Description", Prompt = "Describe your listing")]
        public string? Description { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link 2", Prompt = "Link 2")]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link 3", Prompt = "Link 3")]
        public string? Link3 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Proof Link", Prompt = "Where it shows acceptance on site if NOT on main link")]
        public string? ProofLink { get; set; }

        [MaxLength(500)]
        [Display(Name = "Video Link", Prompt = "Video link of using the site")]
        public string? VideoLink { get; set; }

        [MaxLength(75)]
        [Display(Name = "Contact", Prompt = "@yourname on Twitter/ GitHub, etc.")]
        public string? Contact { get; set; }

        [MaxLength(75)]
        [Display(Name = "Location", Prompt = "City, Region (example: Miami Beach, Florida)")]
        public string? Location { get; set; }

        [Display(Name = "Country")]
        [MaxLength(2)]
        public string? CountryCode { get; set; }

        [MaxLength(5000)]
        [Display(Name = "PGP Key", Prompt = "PGP Key")]
        public string? PgpKey { get; set; }

        [MaxLength(75)]
        [Display(Name = "Processor", Prompt = "Payment processing company/ plugin")]
        public string? Processor { get; set; }

        [MaxLength(255)]
        [Display(Name = "Note", Prompt = "Notes about listing you want displayed")]
        public string? Note { get; set; }

        [MaxLength(500)]
        [Display(Name = "Note To Admin", Prompt = "Notes to admin reviewing submission")]
        public string? NoteToAdmin { get; set; }

        [Display(Name = "SubCategoryId", Prompt = "Select subcategory")]
        public int? SubCategoryId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Suggested Category", Prompt = "New Category > New Subcategory")]
        public string? SuggestedSubCategory { get; set; }

        public int? DirectoryEntryId { get; set; }

        [Display(Name = "Suggested Status", Prompt = "Status")]
        public DirectoryStatus? DirectoryStatus { get; set; }

        [MaxLength(255)]
        [Display(Name = "Tags", Prompt = "comma-separated, e.g. vpn, privacy")]
        public string? Tags { get; set; }

        public List<int> SelectedTagIds { get; set; } = new ();

        [MaxLength(2000)]
        public string? SelectedTagIdsCsv { get; set; }

        [MaxLength(500)]
        [Display(Name = "Related Link 1", Prompt = "Optional")]
        public string? RelatedLink1 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Related Link 2", Prompt = "Optional")]
        public string? RelatedLink2 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Related Link 3", Prompt = "Optional")]
        public string? RelatedLink3 { get; set; }

        [MaxLength(4)]
        public string? FoundedYear { get; set; }

        [MaxLength(2)]
        public string? FoundedMonth { get; set; }

        [MaxLength(2)]
        public string? FoundedDay { get; set; }

        public List<string> GetRelatedLinksNormalized(int max = 3)
        {
            return new[] { this.RelatedLink1, this.RelatedLink2, this.RelatedLink3 }
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

    }
}