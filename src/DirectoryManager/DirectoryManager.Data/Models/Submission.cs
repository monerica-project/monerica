using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class Submission : StateInfo
    {
        private string? description;
        private string? note;

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
        public string? SourceCodeLink { get; set; }

        [MaxLength(500)]
        public string? Description { get => this.description; set => this.description = DirectoryManager.Data.Helpers.ListingText.NormalizeLineBreaks(value); }

        /// <summary>Street address line(s), only for a real physical location (optional).</summary>
        [MaxLength(255)]
        public string? StreetAddress { get; set; }

        [MaxLength(255)]
        public string? City { get; set; }

        /// <summary>State, province or region (shown as "State/Region"). Not the country: that is CountryCode.</summary>
        [MaxLength(255)]
        public string? StateRegion { get; set; }

        [MaxLength(32)]
        public string? PostalCode { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get => this.note; set => this.note = DirectoryManager.Data.Helpers.ListingText.NormalizeLineBreaks(value); }

        public string? PgpKey { get; set; }

        /// <summary>Database limit for <see cref="NoteToAdmin"/> (the public form allows less).</summary>
        public const int NoteToAdminDbMaxLength = 1000;

        [MaxLength(NoteToAdminDbMaxLength, ErrorMessage = "The note to admin can be at most {1} characters. Please shorten it.")]
        public string? NoteToAdmin { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Messenger { get; set; }

        [MaxLength(255)]
        public string? Social { get; set; }

        public int? SubCategoryId { get; set; }

        public Subcategory? SubCategory { get; set; }

        [MaxLength(255)]
        public string? SuggestedSubCategory { get; set; }

        public int? DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public DirectoryStatus? DirectoryStatus { get; set; }

        public KycPolicy? KycPolicy { get; set; }

        public Liquidity Liquidity { get; set; }

        public RateType RateType { get; set; }

        /// <summary>Whether the website needs JavaScript. Unknown = Not Stated.</summary>
        public SiteJavaScript SiteJavaScript { get; set; }

        /// <summary>JSON carrier for the proposed deposit guarantees (see <see cref="Guarantees"/>).</summary>
        public string? GuaranteesJson { get; set; }

        [MaxLength(255)]
        public string? Tags { get; set; }

        [MaxLength(2)]
        public string? CountryCode { get; set; }

        [MaxLength(2000)]
        public string? SelectedTagIdsCsv { get; set; }

        public string? RelatedLinksJson { get; set; }

        public DateOnly? FoundedDate { get; set; }

        // ---- Optional "help cover our review costs" donation, tied to this submission ----
        // Unguessable public token for the pay link (unique). Mirrors the verification-request
        // flow; funds go to the same review-donations BTCPay store.
        public Guid PaymentToken { get; set; }

        [MaxLength(100)]
        public string? BtcPayInvoiceId { get; set; }

        public DateTime? PaidUtc { get; set; }

        public decimal? PaidAmount { get; set; }

        [MaxLength(10)]
        public string? PaidCurrency { get; set; }

        // The donation's value in USD when it was paid (USD invoices: the amount; XMR top-ups:
        // XMR received x BTCPay's locked rate). Used by the revenue report.
        public decimal? PaidUsd { get; set; }

        // XMR actually received for this donation (from BTCPay), whatever currency it was priced in.
        // Shown when the admin revenue page is switched to XMR. Null = not recorded.
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "numeric(20,12)")]
        public decimal? PaidXmr { get; set; }

        // ---- Automated review (DirectoryManager.SubmissionReviewer) ----
        // The submitted fields above are never changed by the reviewer. Its suggested version of the
        // listing goes into the Proposed* fields; OriginalSnapshotJson keeps a copy of what was
        // submitted, so the original survives even after an admin edits the fields above.
        public SubmissionReviewState ReviewState { get; set; } = SubmissionReviewState.Pending;

        public string? OriginalSnapshotJson { get; set; }

        [MaxLength(255)]
        public string? ProposedName { get; set; }

        [MaxLength(DirectoryManager.Data.Helpers.ListingText.DescriptionMaxLength)]
        public string? ProposedDescription { get; set; }

        [MaxLength(DirectoryManager.Data.Helpers.ListingText.NoteMaxLength)]
        public string? ProposedNote { get; set; }

        public int? ProposedSubCategoryId { get; set; }

        // Other subcategories that fit, by name (suggestions only; added by hand).
        [MaxLength(500)]
        public string? ProposedOtherSubcategories { get; set; }

        [MaxLength(2)]
        public string? ProposedCountryCode { get; set; }

        [MaxLength(255)]
        public string? ProposedStreetAddress { get; set; }

        [MaxLength(255)]
        public string? ProposedCity { get; set; }

        [MaxLength(255)]
        public string? ProposedStateRegion { get; set; }

        [MaxLength(32)]
        public string? ProposedPostalCode { get; set; }

        public KycPolicy? ProposedKycPolicy { get; set; }

        public Liquidity? ProposedLiquidity { get; set; }

        public RateType? ProposedRateType { get; set; }

        public SiteJavaScript? ProposedSiteJavaScript { get; set; }

        [MaxLength(255)]
        public string? ProposedTags { get; set; }

        // JSON list of { field, from, to, reason } for every field the reviewer changed or checked.
        public string? ProposedChangesJson { get; set; }

        // Verdict as returned by the model (raw JSON) plus the parsed headline fields.
        public string? ReviewVerdictJson { get; set; }

        [MaxLength(30)]
        public string? ReviewVerdict { get; set; }

        [Column(TypeName = "numeric(4,3)")]
        public decimal? ReviewConfidence { get; set; }

        [MaxLength(30)]
        public string? ReviewMoneroEvidence { get; set; }

        [MaxLength(1000)]
        public string? ReviewSummary { get; set; }

        // JSON list of issue strings.
        public string? ReviewIssuesJson { get; set; }

        [MaxLength(500)]
        public string? ReviewDuplicateOf { get; set; }

        public bool? ReviewBannedCategory { get; set; }

        // Pre-filter outcome and reason (e.g. "AutoReject: URL unreachable (DNS)").
        [MaxLength(500)]
        public string? ReviewPrefilterReason { get; set; }

        [MaxLength(2000)]
        public string? ReviewError { get; set; }

        // The text the model saw (kept so an admin can check what the verdict was based on).
        public string? ReviewDossier { get; set; }

        [MaxLength(100)]
        public string? ReviewModel { get; set; }

        public int? ReviewTokensIn { get; set; }

        public int? ReviewTokensOut { get; set; }

        public DateTime? ReviewedUtc { get; set; }

        public int ReviewAttempts { get; set; }

        // Append-only audit trail of reviewer verdicts and admin actions ("2026-11-08 14:02Z AI: ...").
        public string? ReviewLog { get; set; }

        public void AppendReviewLog(string entry)
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z {entry}";
            this.ReviewLog = string.IsNullOrEmpty(this.ReviewLog) ? line : this.ReviewLog + "\n" + line;
        }

        public Submission ShallowCopy() => (Submission)this.MemberwiseClone();

        /// <summary>JSON copy of the submitted fields (kept in <see cref="OriginalSnapshotJson"/> before anyone edits them).</summary>
        public string CreateSnapshotJson() => JsonSerializer.Serialize(new
        {
            this.Name,
            this.Link,
            this.Link2,
            this.Link3,
            this.ProofLink,
            this.VideoLink,
            this.SourceCodeLink,
            this.Description,
            this.StreetAddress,
            this.City,
            this.StateRegion,
            this.PostalCode,
            this.Processor,
            this.Note,
            this.NoteToAdmin,
            this.Email,
            this.Messenger,
            this.Social,
            this.SubCategoryId,
            this.SuggestedSubCategory,
            this.DirectoryEntryId,
            DirectoryStatus = this.DirectoryStatus?.ToString(),
            KycPolicy = this.KycPolicy?.ToString(),
            Liquidity = this.Liquidity.ToString(),
            RateType = this.RateType.ToString(),
            SiteJavaScript = this.SiteJavaScript.ToString(),
            this.GuaranteesJson,
            this.Tags,
            this.CountryCode,
            this.SelectedTagIdsCsv,
            this.RelatedLinksJson,
            this.FoundedDate,
            this.PgpKey,
        });

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

        /// <summary>
        /// Proposed deposit guarantees (link + USD amount), backed by <see cref="GuaranteesJson"/>.
        /// Normalized on read/write: blank links or non-positive amounts are dropped, capped at 4
        /// (IntegerConstants.MaxGuarantees).
        /// </summary>
        [NotMapped]
        public List<GuaranteeItem> Guarantees
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this.GuaranteesJson))
                {
                    return new List<GuaranteeItem>();
                }

                try
                {
                    var list = JsonSerializer.Deserialize<List<GuaranteeItem>>(this.GuaranteesJson)
                               ?? new List<GuaranteeItem>();

                    return NormalizeGuarantees(list);
                }
                catch
                {
                    return new List<GuaranteeItem>();
                }
            }

            set
            {
                var normalized = NormalizeGuarantees(value);

                this.GuaranteesJson = normalized.Count == 0
                    ? null
                    : JsonSerializer.Serialize(normalized);
            }
        }

        private static List<GuaranteeItem> NormalizeGuarantees(IEnumerable<GuaranteeItem>? items)
        {
            return (items ?? new List<GuaranteeItem>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Link) && x.Amount > 0)
                .Select(x => new GuaranteeItem { Link = (x.Link ?? string.Empty).Trim(), Amount = x.Amount, Currency = x.Currency })
                .Take(4)
                .ToList();
        }
    }
}
