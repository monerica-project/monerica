using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.ModelBinding;

namespace DirectoryManager.Web.Models
{
    public class SubmissionRequest : IValidatableObject
    {
        public int? SubmissionId { get; set; }

        // URL — validated by UrlHelper in the controller; do NOT Unicode-clean.
        [Required]
        [MaxLength(500)]
        [Display(Name = "Link", Prompt = "https://yoursite.net")]
        public string Link { get; set; } = string.Empty;

        [Required]
        [MaxLength(65)]
        [Display(Name = "Name", Prompt = "Company or project name")]
        [CleanSingleLine]
        public string Name { get; set; } = string.Empty;

        [MaxLength(175)]
        [Display(Name = "Description", Prompt = "Describe your listing")]
        [CleanMultiLine]
        public string? Description { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Link 2", Prompt = "Link 2")]
        public string? Link2 { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Link 3", Prompt = "Link 3")]
        public string? Link3 { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Proof Link", Prompt = "Where it shows acceptance on site if NOT on main link")]
        public string? ProofLink { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Video Link", Prompt = "Video link of using the site")]
        public string? VideoLink { get; set; }


        [MaxLength(255)]
        [Display(Name = "Email", Prompt = "contact@example.com")]
        [CleanSingleLine]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Display(Name = "Messenger", Prompt = "@handle on Telegram, Signal, etc.")]
        [CleanSingleLine]
        public string? Messenger { get; set; }

        [MaxLength(255)]
        [Display(Name = "Social", Prompt = "@handle on X, Nostr, etc.")]
        [CleanSingleLine]
        public string? Social { get; set; }

        [MaxLength(75)]
        [Display(Name = "Location", Prompt = "City, Region (example: Miami Beach, Florida)")]
        [CleanSingleLine]
        public string? Location { get; set; }

        // 2-letter country code — leave raw.
        [Display(Name = "Country")]
        [MaxLength(2)]
        public string? CountryCode { get; set; }

        // ASCII-armored PGP key — validated by PgpKeyValidator; MUST stay raw.
        [Display(Name = "PGP Key", Prompt = "PGP Key")]
        public string? PgpKey { get; set; }

        [MaxLength(75)]
        [Display(Name = "Processor", Prompt = "Payment processing company/ plugin")]
        [CleanSingleLine]
        public string? Processor { get; set; }

        [MaxLength(255)]
        [Display(Name = "Note", Prompt = "Notes about listing you want displayed")]
        [CleanMultiLine]
        public string? Note { get; set; }

        [MaxLength(500)]
        [Display(Name = "Note To Admin", Prompt = "Notes to admin reviewing submission")]
        [CleanMultiLine]
        public string? NoteToAdmin { get; set; }

        [Display(Name = "SubCategoryId", Prompt = "Select subcategory")]
        public int? SubCategoryId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Suggested Category", Prompt = "New Category > New Subcategory")]
        [CleanSingleLine]
        public string? SuggestedSubCategory { get; set; }

        public int? DirectoryEntryId { get; set; }

        [Display(Name = "Suggested Status", Prompt = "Status")]
        public DirectoryStatus? DirectoryStatus { get; set; }

        [MaxLength(255)]
        [Display(Name = "Tags", Prompt = "comma-separated, e.g. vpn, privacy")]
        [CleanSingleLine]
        public string? Tags { get; set; }

        public List<int> SelectedTagIds { get; set; } = new ();

        [MaxLength(2000)]
        public string? SelectedTagIdsCsv { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Related Link 1", Prompt = "Optional")]
        public string? RelatedLink1 { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Related Link 2", Prompt = "Optional")]
        public string? RelatedLink2 { get; set; }

        // URL
        [MaxLength(500)]
        [Display(Name = "Related Link 3", Prompt = "Optional")]
        public string? RelatedLink3 { get; set; }

        [MaxLength(4)]
        public string? FoundedYear { get; set; }

        [MaxLength(2)]
        public string? FoundedMonth { get; set; }

        [MaxLength(2)]
        public string? FoundedDay { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            => InputHtmlGuard.Validate(this);

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
