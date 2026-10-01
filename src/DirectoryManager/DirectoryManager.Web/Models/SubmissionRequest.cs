using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Attributes;
using DirectoryManager.Web.Helpers;
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

        /// <summary>
        /// Max length of the listing description. Kept short by design so listing cards stay
        /// compact; the DB column allows more, but 175 is the intended editorial limit and is
        /// enforced identically on the admin edit form (see DirectoryEntryEditViewModel).
        /// </summary>
        public const int DescriptionMaxLength = 175;

        // No [MaxLength] here on purpose: a hard HTML maxlength silently truncates a pasted
        // description (and blocks editing existing longer listings). The length is enforced in
        // the controller with a clear over-length error instead (see SubmissionController), so
        // nothing is cut off silently — same pattern as Note below.
        [Required(ErrorMessage = "Please add a short description of your listing.")]
        [Display(Name = "Description", Prompt = "Describe your listing")]
        [CleanMultiLine]
        public string Description { get; set; } = string.Empty;

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

        // URL
        [MaxLength(500)]
        [Display(Name = "Source Code Link", Prompt = "Link to the source code, e.g. GitHub or Codeberg")]
        public string? SourceCodeLink { get; set; }

        [MaxLength(255)]
        [Display(Name = "Email", Prompt = "contact@example.com")]
        [CleanSingleLine]
        [UrlOrEmail(AllowUrl = false)]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Display(Name = "Messenger", Prompt = "Full link, e.g. https://t.me/yourname or https://signal.me/...")]
        [CleanSingleLine]
        [UrlOrEmail]
        public string? Messenger { get; set; }

        [MaxLength(255)]
        [Display(Name = "Social", Prompt = "Full link, e.g. https://x.com/yourname")]
        [CleanSingleLine]
        [UrlOrEmail]
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
        // [AllowHtml] exempts it from the HTML guard (armored keys can carry angle
        // brackets, e.g. a Comment/UID email); Validate() below still requires it to
        // be a genuine PGP public key so the exemption can't smuggle markup.
        [AllowHtml]
        [Display(Name = "PGP Key", Prompt = "PGP Key")]
        public string? PgpKey { get; set; }

        [MaxLength(75)]
        [Display(Name = "Processor", Prompt = "Payment processing company/ plugin")]
        [CleanSingleLine]
        public string? Processor { get; set; }

        /// <summary>
        /// Max length of the public note shown on the listing. Kept short by design (editorial
        /// limit, well under the DB column) so notes stay concise; enforced identically on the
        /// admin edit form (see DirectoryEntryEditViewModel).
        /// </summary>
        public const int NoteMaxLength = 250;

        // No [MaxLength] here on purpose: that renders a hard HTML maxlength that silently
        // truncates a pasted note. The length is enforced in the controller with a clear
        // over-length error instead (see SubmissionController), so nothing is cut off silently.
        [Display(Name = "Note", Prompt = "Coupon/discount codes, fees, or special ordering steps shown on your listing — e.g. 'Use code XMR10 for 10% off', 'Pay by email', or 'Minimum order $50'")]
        [CleanMultiLine]
        public string? Note { get; set; }

        /// <summary>Max length of the private note the submitter sends to the reviewer/admin.</summary>
        public const int NoteToAdminMaxLength = 500;

        [StringLength(NoteToAdminMaxLength, ErrorMessage = "The note to the admin can be at most {1} characters. Please shorten your message.")]
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

        [Display(Name = "KYC Policy")]
        public KycPolicy? KycPolicy { get; set; }

        // Liquidity + deposit guarantees — primarily for instant-swap exchanges; all optional.
        // Liquidity defaults to NotApplicable (most listings). Guarantees is a list (up to 4) of
        // link + amount + currency, bound from indexed form fields (Guarantees[i].Link / .Amount /
        // .Currency) and validated in the controller (a link and an amount are required together).
        [Display(Name = "Liquidity")]
        public Liquidity Liquidity { get; set; } = Liquidity.NotApplicable;

        public List<DirectoryManager.Data.Models.GuaranteeItem> Guarantees { get; set; } = new ();

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
        {
            var results = new List<ValidationResult>(InputHtmlGuard.Validate(this));

            // PgpKey is [AllowHtml]-exempt from the guard, so require it to be a genuine
            // ASCII-armored PGP public key — the exemption must not smuggle real markup.
            if (!string.IsNullOrWhiteSpace(this.PgpKey) && PgpKeyValidator.IsValid(this.PgpKey)
                && !PgpCapabilities.HasUsableEncryptionKey(this.PgpKey))
            {
                // Ownership/authorship is verified by encrypting a one-time code to the key, so a
                // sign-only or certify-only key can't be used. Reject it here rather than accepting it
                // into the listing and failing later at the challenge step.
                results.Add(new ValidationResult(
                    "This PGP key has no usable encryption subkey, so it can't be used to verify " +
                    "ownership (verification works by encrypting a one-time code to the key). Please " +
                    "provide a key with a current, non-revoked encryption subkey — a sign-only or " +
                    "certify-only key won't work.",
                    new[] { nameof(this.PgpKey) }));
            }

            if (!string.IsNullOrWhiteSpace(this.PgpKey) && !PgpKeyValidator.IsValid(this.PgpKey))
            {
                results.Add(new ValidationResult(
                    "The PGP public key block you entered is not valid. " +
                    "Please supply a valid ASCII-armored PGP public key.",
                    new[] { nameof(this.PgpKey) }));
            }

            // Descriptions must read as clean directory copy: no sentence may end with an
            // exclamation mark. This flags a "!" that terminates a sentence (end of the text, or
            // before whitespace, allowing closing quotes/brackets like great!") while still
            // permitting a "!" that is part of a name mid-word (e.g. "Yahoo!Store").
            if (!string.IsNullOrWhiteSpace(this.Description) &&
                System.Text.RegularExpressions.Regex.IsMatch(this.Description, "!+[\"')\\]”’]*(?=\\s|$)"))
            {
                results.Add(new ValidationResult(
                    "Sentences in the description can't end with an exclamation mark (\"!\"). " +
                    "Please rephrase so no sentence ends with \"!\".",
                    new[] { nameof(this.Description) }));
            }

            // The proof link is only useful when it points to a DIFFERENT page than the main link.
            // Submitters keep pasting the same URL for both; reject that so they either provide a
            // real separate proof page or leave it blank.
            if (!string.IsNullOrWhiteSpace(this.ProofLink) && !string.IsNullOrWhiteSpace(this.Link)
                && NormalizeUrlForCompare(this.ProofLink) == NormalizeUrlForCompare(this.Link))
            {
                results.Add(new ValidationResult(
                    "The proof link is the same as the main link. Only add a proof link when it's a " +
                    "different page than the main link — otherwise leave it blank.",
                    new[] { nameof(this.ProofLink) }));
            }

            return results;
        }

        // Loosely normalizes a URL so trivially-different forms of the same page compare equal:
        // lowercased, scheme dropped, a leading "www." dropped, and trailing slashes trimmed.
        // Paths are preserved, so a genuinely different proof page still differs from the main link.
        private static string NormalizeUrlForCompare(string? url)
        {
            var s = (url ?? string.Empty).Trim().ToLowerInvariant();
            if (s.StartsWith("https://"))
            {
                s = s.Substring(8);
            }
            else if (s.StartsWith("http://"))
            {
                s = s.Substring(7);
            }

            if (s.StartsWith("www."))
            {
                s = s.Substring(4);
            }

            return s.TrimEnd('/');
        }

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
