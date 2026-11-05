using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>A listing shown in a sponsor spot on /sponsors-and-featured.</summary>
    public class SpotListing
    {
        public string Name { get; set; } = string.Empty;

        public string ProfilePath { get; set; } = string.Empty;

        public DirectoryStatus DirectoryStatus { get; set; }

        public int DirectoryEntryId { get; set; }

        public int SubCategoryId { get; set; }

        public string? Description { get; set; }

        public string? CountryCode { get; set; }

        public string? CountryName { get; set; }

        /// <summary>"Category › Subcategory" where the listing sits.</summary>
        public string? ListedIn { get; set; }

        public string? KycLabel { get; set; }

        public string? LiquidityLabel { get; set; }

        public string? RateLabel { get; set; }

        public int? FoundedYear { get; set; }

        public string? WebsiteUrl { get; set; }

        public string? TorUrl { get; set; }

        public double? AvgRating { get; set; }

        public int ReviewCount { get; set; }

        public int VerifiedOrderCount { get; set; }
    }
}
