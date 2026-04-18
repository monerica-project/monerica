using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class WaitlistScopedItemDto
    {
        public int SponsoredListingOpeningNotificationId { get; set; }
        public SponsorshipType SponsorshipType { get; set; }
        public int? TypeId { get; set; }
        public int? DirectoryEntryId { get; set; }

        // ✅ joined = SubscribedDate
        public DateTime SubscribedDateUtc { get; set; }
    }
}