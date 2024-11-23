using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class AdSpotNotificationSubscription
    {
        public int AdSpotNotificationSubscriptionId { get; set; }

        [ForeignKey("EmailSubscription")]
        public int EmailSubscriptionId { get; set; }
        public EmailSubscription EmailSubscription { get; set; } = null!;

        public bool NotifyOnExpiry { get; set; } = false;
        public bool NotifyOnOpening { get; set; } = false;

        public SponsorshipType PreferredSponsorshipType { get; set; } = SponsorshipType.Unknown;
    }
}