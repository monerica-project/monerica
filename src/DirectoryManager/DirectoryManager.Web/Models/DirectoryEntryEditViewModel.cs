using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Attributes;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryEditViewModel : IValidatableObject
    {
        // Length limits match the public SubmissionRequest so admin edit and public submit
        // validate identically. Both are editorial caps (well under the DB columns) that keep
        // listing cards compact.
        public const int DescriptionMaxLength = 175;   // editorial limit (mirrors SubmissionRequest)
        public const int NoteMaxLength = 250;          // editorial limit (mirrors SubmissionRequest)

        public int DirectoryEntryId { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        [Display(Name = "KYC Policy")]
        public KycPolicy? KycPolicy { get; set; }

        [Display(Name = "Liquidity")]
        public Liquidity Liquidity { get; set; } = Liquidity.NotApplicable;

        public List<DirectoryManager.Data.Models.GuaranteeItem> Guarantees { get; set; } = new ();

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
        public string? SourceCodeLink { get; set; }
        public string? Location { get; set; }
        public string? CountryCode { get; set; }
        public string? Processor { get; set; }
        [UrlOrEmail(AllowUrl = false)]
        public string? Email { get; set; }

        [UrlOrEmail]
        public string? Messenger { get; set; }

        [UrlOrEmail]
        public string? Social { get; set; }
        [Required(ErrorMessage = "A description is required.")]
        public string Description { get; set; } = string.Empty;
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

            // Description / Note length: no hard HTML maxlength on these fields (so a long value
            // is never silently truncated) — surface a clear over-length error instead, at the
            // same limits the public submit enforces. Keeps admin edit and submit in lockstep.
            if (!string.IsNullOrEmpty(this.Description) && this.Description.Length > DescriptionMaxLength)
            {
                results.Add(new ValidationResult(
                    $"The description is {this.Description.Length} characters, which is over the {DescriptionMaxLength}-character limit. Please shorten it.",
                    new[] { nameof(this.Description) }));
            }

            if (!string.IsNullOrEmpty(this.Note) && this.Note.Length > NoteMaxLength)
            {
                results.Add(new ValidationResult(
                    $"The note is {this.Note.Length} characters, which is over the {NoteMaxLength}-character limit. Please shorten it.",
                    new[] { nameof(this.Note) }));
            }

            // PgpKey is [AllowHtml]-exempt from the guard, so require it to be a genuine
            // ASCII-armored PGP public key — the exemption must not smuggle real markup.
            if (!string.IsNullOrWhiteSpace(this.PgpKey))
            {
                if (!PgpKeyValidator.IsValid(this.PgpKey))
                {
                    results.Add(new ValidationResult(
                        "The PGP public key block you entered is not valid. " +
                        "Please supply a valid ASCII-armored PGP public key.",
                        new[] { nameof(this.PgpKey) }));
                }
                else if (!PgpCapabilities.HasUsableEncryptionKey(this.PgpKey))
                {
                    // Ownership is verified by encrypting a one-time code to the key, so it needs a
                    // usable encryption (sub)key. Reject sign-only keys up front instead of accepting
                    // them and failing later at the challenge step.
                    results.Add(new ValidationResult(
                        "This PGP key has no usable encryption subkey, so it can't be used to verify " +
                        "ownership (verification works by encrypting a one-time code to the key). Please " +
                        "provide a key with a current, non-revoked encryption subkey — a sign-only or " +
                        "certify-only key won't work.",
                        new[] { nameof(this.PgpKey) }));
                }
            }

            return results;
        }
    }
}