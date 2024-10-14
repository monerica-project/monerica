using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Enums;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryViewModel
    {
        public int DirectoryEntryId { get; set; }

        [Required]
        [MaxLength(255)]
        required public string Name { get; set; }

        [MaxLength(255)]
        public string DirectoryEntryKey { get; set; } = string.Empty;

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Affiliate link of main link.
        /// </summary>
        [MaxLength(500)]
        public string? LinkA { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        /// <summary>
        /// Affiliate link of second link.
        /// </summary>
        [MaxLength(500)]
        public string? Link2A { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        /// <summary>
        /// Affiliate link of third link.
        /// </summary>
        [MaxLength(500)]
        public string? Link3A { get; set; }

        [Required]
        public DirectoryStatus DirectoryStatus { get; set; }

        public DirectoryBadge DirectoryBadge { get; set; } = DirectoryBadge.Unknown;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        [MaxLength(255)]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [MaxLength(75)]
        public string? Contact { get; set; }

        public virtual Subcategory? SubCategory { get; set; }

        public int? SubCategoryId { get; set; }

        public DateDisplayOption DateOption { get; set; } = DateDisplayOption.NotDisplayed;

        public bool IsSponsored { get; set; } = false;

        public bool IsSubCategorySponsor { get; set; } = false;

        public bool DisplayAsSponsoredItem { get; set; } = false;

        public string Link2Name { get; set; } = "Link 2";

        public string Link3Name { get; set; } = "Link 3";

        public DateTime? UpdateDate { get; set; }

        public DateTime CreateDate { get; set; }

        public LinkType LinkType { get; set; } = LinkType.Direct;

        public ItemDisplayType ItemDisplayType { get; set; } = ItemDisplayType.Normal;

        public string ItemPath { get; set; } = string.Empty;
    }
}