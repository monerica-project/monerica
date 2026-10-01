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
        public string? SourceCodeLink { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        public string? PgpKey { get; set; }

        [MaxLength(1000)]
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

        [MaxLength(255)]
        public string? IpAddress { get; set; }

        public int? DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public DirectoryStatus? DirectoryStatus { get; set; }

        public KycPolicy? KycPolicy { get; set; }

        public Liquidity Liquidity { get; set; }

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
