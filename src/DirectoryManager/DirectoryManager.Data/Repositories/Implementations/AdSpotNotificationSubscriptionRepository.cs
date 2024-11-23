using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class AdSpotNotificationSubscriptionRepository : IAdSpotNotificationSubscriptionRepository
    {
        private readonly IApplicationDbContext context;

        public AdSpotNotificationSubscriptionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public AdSpotNotificationSubscription? Get(int emailSubscriptionId)
        {
            return this.context.AdSpotNotificationSubscriptions
                          .FirstOrDefault(n => n.EmailSubscriptionId == emailSubscriptionId);
        }

        public void UpdateNotificationPreferences(int emailSubscriptionId, bool notifyOnExpiry, bool notifyOnOpening, SponsorshipType sponsorshipType)
        {
            var subscription = this.Get(emailSubscriptionId);

            if (subscription == null)
            {
                subscription = new AdSpotNotificationSubscription
                {
                    EmailSubscriptionId = emailSubscriptionId,
                    NotifyOnExpiry = notifyOnExpiry,
                    NotifyOnOpening = notifyOnOpening,
                    PreferredSponsorshipType = sponsorshipType
                };
                this.context.AdSpotNotificationSubscriptions.Add(subscription);
            }
            else
            {
                subscription.NotifyOnExpiry = notifyOnExpiry;
                subscription.NotifyOnOpening = notifyOnOpening;
                subscription.PreferredSponsorshipType = sponsorshipType;
                this.context.AdSpotNotificationSubscriptions.Update(subscription);
            }

            this.context.SaveChanges();
        }

        public IEnumerable<AdSpotNotificationSubscription> GetSubscribersForExpiry()
        {
            return this.context.AdSpotNotificationSubscriptions
                          .Where(n => n.NotifyOnExpiry && n.EmailSubscription.IsSubscribed)
                          .Include(n => n.EmailSubscription)
                          .ToList();
        }

        public IEnumerable<AdSpotNotificationSubscription> GetSubscribersForOpening(SponsorshipType sponsorshipType)
        {
            return this.context.AdSpotNotificationSubscriptions
                          .Where(n => n.NotifyOnOpening && n.EmailSubscription.IsSubscribed && n.PreferredSponsorshipType == sponsorshipType)
                          .Include(n => n.EmailSubscription)
                          .ToList();
        }
    }
}