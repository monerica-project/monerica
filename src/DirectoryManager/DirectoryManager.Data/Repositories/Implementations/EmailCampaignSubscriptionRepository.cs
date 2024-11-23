using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailCampaignSubscriptionRepository : IEmailCampaignSubscriptionRepository
    {
        private readonly IApplicationDbContext context;

        public EmailCampaignSubscriptionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public EmailCampaignSubscription? Get(int subscriptionId)
        {
            return this.context.EmailCampaignSubscriptions
                        .Include(e => e.EmailCampaign)
                        .Include(e => e.EmailSubscription)
                        .FirstOrDefault(e => e.EmailCampaignSubscriptionId == subscriptionId);
        }

        public IEnumerable<EmailCampaignSubscription> GetByCampaign(int campaignId)
        {
            return this.context.EmailCampaignSubscriptions
                        .Include(e => e.EmailSubscription)
                        .Where(e => e.EmailCampaignId == campaignId && e.IsActive)
                        .ToList();
        }

        public IEnumerable<EmailCampaignSubscription> GetByEmailSubscription(int emailSubscriptionId)
        {
            return this.context.EmailCampaignSubscriptions
                        .Include(e => e.EmailCampaign)
                        .Where(e => e.EmailSubscriptionId == emailSubscriptionId && e.IsActive)
                        .ToList();
        }

        public EmailCampaignSubscription SubscribeToCampaign(int campaignId, int emailSubscriptionId)
        {
            // Check if the subscription already exists
            var existingSubscription = this.context.EmailCampaignSubscriptions
                .FirstOrDefault(e => e.EmailCampaignId == campaignId && e.EmailSubscriptionId == emailSubscriptionId);

            if (existingSubscription != null && existingSubscription.IsActive)
            {
                return existingSubscription; // Already subscribed and active
            }

            // Create a new subscription if it doesn't exist or reactivate if inactive
            if (existingSubscription == null)
            {
                existingSubscription = new EmailCampaignSubscription
                {
                    EmailCampaignId = campaignId,
                    EmailSubscriptionId = emailSubscriptionId,
                    IsActive = true,
                    SubscribedDate = DateTime.UtcNow
                };

                this.context.EmailCampaignSubscriptions.Add(existingSubscription);
            }
            else
            {
                existingSubscription.IsActive = true;
                existingSubscription.SubscribedDate = DateTime.UtcNow;
                this.context.EmailCampaignSubscriptions.Update(existingSubscription);
            }

            this.context.SaveChanges();
            return existingSubscription;
        }

        public bool UnsubscribeFromCampaign(int campaignId, int emailSubscriptionId)
        {
            var subscription = this.context.EmailCampaignSubscriptions
                .FirstOrDefault(e => e.EmailCampaignId == campaignId && e.EmailSubscriptionId == emailSubscriptionId);

            if (subscription == null || !subscription.IsActive)
            {
                return false;
            }

            subscription.IsActive = false;
            this.context.EmailCampaignSubscriptions.Update(subscription);
            this.context.SaveChanges();
            return true;
        }

        public bool IsSubscribed(int campaignId, int emailSubscriptionId)
        {
            return this.context.EmailCampaignSubscriptions
                .Any(e => e.EmailCampaignId == campaignId && e.EmailSubscriptionId == emailSubscriptionId && e.IsActive);
        }

        public int TotalSubscriptionsForCampaign(int campaignId)
        {
            return this.context.EmailCampaignSubscriptions
                .Count(e => e.EmailCampaignId == campaignId && e.IsActive);
        }

        public IEnumerable<EmailSubscription> GetActiveSubscribers(int campaignId)
        {
            return this.context.EmailCampaignSubscriptions
                               .Where(s => s.EmailCampaignId == campaignId &&
                                           s.IsActive &&
                                           s.EmailSubscription.IsSubscribed)
                               .Select(s => s.EmailSubscription)
                               .ToList();
        }

        public bool HasReceivedMessage(int emailSubscriptionId, int emailMessageId)
        {
            return this.context.SentEmailRecords
                          .Any(r => r.EmailSubscriptionId == emailSubscriptionId && r.EmailMessageId == emailMessageId);
        }
    }
}
