using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class CurrentSponsorItemVm
    {
        public int DirectoryEntryId { get; set; }

        public string ListingName { get; set; } = string.Empty;

        public string ListingUrl { get; set; } = string.Empty;

        public SponsorshipType SponsorshipTypeEnum { get; set; }
            = Data.Enums.SponsorshipType.Unknown;

        public string SponsorshipType { get; set; } = string.Empty;

        /// <summary>
        /// What the sponsorship covers, without the type prefix: empty for a main
        /// (site-wide) sponsor, the category name for a category sponsor, and the
        /// formatted "Category &gt; Subcategory" for a subcategory sponsor.
        /// </summary>
        public string ScopeLabel { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime ExpiresUtc { get; set; }

        public string RenewUrl { get; set; } = string.Empty;
    }
}
