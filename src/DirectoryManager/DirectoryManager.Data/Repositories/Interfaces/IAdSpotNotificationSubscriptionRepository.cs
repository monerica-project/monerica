using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAdSpotNotificationSubscriptionRepository
    {
        AdSpotNotificationSubscription? Get(int emailSubscriptionId);
        void UpdateNotificationPreferences(int emailSubscriptionId, bool notifyOnExpiry, bool notifyOnOpening, SponsorshipType sponsorshipType);
        IEnumerable<AdSpotNotificationSubscription> GetSubscribersForExpiry();
        IEnumerable<AdSpotNotificationSubscription> GetSubscribersForOpening(SponsorshipType sponsorshipType);
    }
}