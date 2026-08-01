using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryEditViewModel : IValidatableObject
    {
        public int DirectoryEntryId { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        [Display(Name = "KYC Policy")]
        public KycPolicy? KycPolicy { get; set; }

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
        public string? Email { get; set; }
        public string? Messenger { get; set; }
        public string? Social { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }

        // ASCII-armored PGP key — exempt from the HTML guard (armored keys can carry
        // angle brackets, e.g. a Comment/UID email); validated below by PgpKeyValidator.
        [AllowHtml]
        public string? PgpKey { get; set; }

        public string? FoundedYear { get; set; }
        public string? FoundedMonth { get; set; }
        public string? FoundedDay { get; set; }

        public bool ReviewsDisabled { get; set; }

        // Read-only (owner controls these via the PGP /site/{key}/admin page) — shown so the
        // operator can see whether the listing opted into review/reply email notifications.
        public bool ReviewEmailNotificationsEnabled { get; set; }

        public DateTime? ReviewEmailNotificationsEnabledUtc { get; set; }

        // ✅ Existing tags chosen via checkboxes (these are what get persisted)
        public List<int> SelectedTagIds { get; set; } = new ();

        // ✅ Optional: allow creating new tags by typing (also persisted if you want)
        // If you want “create/edit ONLY with checkboxes”, just remove this field & UI.
        [MaxLength(200)]
        public string? NewTagsCsv { get; set; }
        public string DirectoryEntryKey { get; set; } = string.Empty;
        public List<string> AdditionalLinks { get; set; } = new ();

        // Total number of reviews (any moderation status) for this entry.
        public int ReviewCount { get; set; }

        // Rejects HTML/CSS/JS on every string field (incl. Note) unless the
        // property is decorated with [AllowHtml]. Mirrors SubmissionRequest so
        // the admin edit form enforces the same plain-text rule as public submissions.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>(InputHtmlGuard.Validate(this));

            // PgpKey is [AllowHtml]-exempt from the guard, so require it to be a genuine
            // ASCII-armored PGP public key — the exemption must not smuggle real markup.
            if (!string.IsNullOrWhiteSpace(this.PgpKey) && !PgpKeyValidator.IsValid(this.PgpKey))
            {
                results.Add(new ValidationResult(
                    "The PGP public key block you entered is not valid. " +
                    "Please supply a valid ASCII-armored PGP public key.",
                    new[] { nameof(this.PgpKey) }));
            }

            return results;
        }
    }
}