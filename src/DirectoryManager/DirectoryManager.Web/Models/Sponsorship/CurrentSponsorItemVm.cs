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

        public DateTime ExpiresUtc { get; set; }

        public string RenewUrl { get; set; } = string.Empty;
    }
}
