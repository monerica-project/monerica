using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class DirectoryEntriesAudit : UserStateInfo
    {
        [Key]
        public int DirectoryEntriesAuditId { get; set; }

        public int DirectoryEntryId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        [MaxLength(500)]
        public string? ProofLink { get; set; }

        [MaxLength(500)]
        public string? VideoLink { get; set; }

        [MaxLength(500)]
        public string? SourceCodeLink { get; set; }

        [MaxLength(2)]
        public string? CountryCode { get; set; }

        [Column(TypeName = "date")]
        public DateOnly? FoundedDate { get; set; }

        public DirectoryStatus DirectoryStatus { get; set; }

        public KycPolicy? KycPolicy { get; set; }

        public Liquidity Liquidity { get; set; }

        public RateType RateType { get; set; }

        /// <summary>Whether the website needs JavaScript. Unknown = Not Stated.</summary>
        public SiteJavaScript SiteJavaScript { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

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
        public string? Note { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }

        [MaxLength(255)]
        public string? Messenger { get; set; }

        [MaxLength(255)]
        public string? Social { get; set; }

        // ASCII-armored PGP public key at the time of this snapshot. The history view
        // shows only its fingerprint (thumbprint), not the full block.
        public string? PgpKey { get; set; }

        public int? SubCategoryId { get; set; }

        public virtual Subcategory? SubCategory { get; set; }

        [NotMapped]
        public string SubCategoryName { get; set; } = string.Empty;
    }
}
