using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Enums;

namespace DirectoryManager.DisplayFormatting.Models
{
    public class DirectoryEntryViewModel
    {
        public int DirectoryEntryId { get; set; }

        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string DirectoryEntryKey { get; set; } = string.Empty;

        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? LinkA { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        public string? Link2A { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        [MaxLength(500)]
        public string? Link3A { get; set; }

        public DirectoryStatus DirectoryStatus { get; set; } = DirectoryStatus.Unknown;

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

        public DateOnly? FoundedDate { get; set; }

        public LinkType LinkType { get; set; } = LinkType.Direct;

        public ItemDisplayType ItemDisplayType { get; set; } = ItemDisplayType.Normal;

        public string ItemPath { get; set; } = string.Empty;

        public List<string>? Tags { get; set; }
        public Dictionary<string, string>? TagsAndKeys { get; set; }

        public string? CountryCode { get; set; }

        public string? PgpKey { get; set; }

        [MaxLength(500)]
        public string? ProofLink { get; set; }

        [MaxLength(500)]
        public string? VideoLink { get; set; }

        public string? FormattedLocation { get; set; }

        /// <summary>
        /// Average rating (Approved reviews only). Null if no ratings.
        /// </summary>
        public double? AverageRating { get; set; }

        /// <summary>
        /// Count of Approved reviews that have a rating value.
        /// </summary>
        public int? ReviewCount { get; set; }

        public List<string> AdditionalLinks { get; set; } = new ();
    }
}