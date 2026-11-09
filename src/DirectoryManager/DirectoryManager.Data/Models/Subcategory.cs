using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class Subcategory : UserStateInfo
    {
        [Key]
        public int SubCategoryId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string SubCategoryKey { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? MetaDescription { get; set; }

        /// <summary>
        /// Optional hand-written &lt;title&gt; for this page (keep it to 60 characters). When blank,
        /// the title is built by SeoTitleBuilder. Lets a few high-traffic pages match how people
        /// actually search (e.g. "Monero Casinos" for Gambling) without renaming the category.
        /// </summary>
        [MaxLength(70)]
        public string? SeoTitle { get; set; }

        /// <summary>
        /// Optional page heading (H1). When blank, the page uses the generated title without the
        /// site name. Changing it never changes the URL.
        /// </summary>
        [MaxLength(100)]
        public string? Heading { get; set; }

        [MaxLength(2000)]
        public string? PageDetails { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }

        // Foreign Key for Category
        [Required]
        public int CategoryId { get; set; }

        public bool IsEnabled { get; set; }

        // When true, reviews left for entries in this subcategory must include an
        // order-proof URL and are always held for manual moderation (never auto-published).
        // An optional free-text "verification details" field is also surfaced to reviewers.
        public bool RequireReviewVerification { get; set; }

        // When true, listings in this subcategory CANNOT buy sponsorship (any tier). Sponsorship
        // is allowed by default (false) — an admin turns this on for subcategories that are a
        // potential issue. Enforced in the sponsorship checkout flow (SelectDuration).
        public bool SponsorshipDisabled { get; set; }

        /// <summary>
        /// Admin-picked unpaid "Featured" listing shown in this subcategory's sponsor spot when no
        /// subcategory sponsor is active. Only shown while that listing is Admitted or Verified.
        /// </summary>
        public int? FeaturedDirectoryEntryId { get; set; }

        // Navigation Property for the parent Category
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
    }
}
