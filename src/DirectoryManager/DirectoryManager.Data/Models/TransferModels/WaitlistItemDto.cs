using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class WaitlistItemDto
    {
        public int SponsoredListingOpeningNotificationId { get; set; }

        public string Email { get; set; } = string.Empty;

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;

        public int? TypeId { get; set; }

        public int? DirectoryEntryId { get; set; }

        // “Live” display fields (what your UI should use)
        public string? ListingName { get; set; }
        public string? ListingUrl { get; set; }

        // Back-compat fields (in case some code still references snapshot names)
        public string? ListingNameSnapshot { get; set; }
        public string? ListingUrlSnapshot { get; set; }

        // Your controller/view currently expects this name
        public DateTime CreateDateUtc { get; set; }
    }
}
