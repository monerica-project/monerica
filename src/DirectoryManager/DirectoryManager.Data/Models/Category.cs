using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class Category : UserStateInfo
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string CategoryKey { get; set; } = string.Empty;

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
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }

        public bool IsEnabled { get; set; }

        /// <summary>
        /// Admin-picked unpaid "Featured" listing shown in this category's sponsor spot when no
        /// category sponsor is active. Only shown while that listing is Admitted or Verified.
        /// </summary>
        public int? FeaturedDirectoryEntryId { get; set; }
    }
}
