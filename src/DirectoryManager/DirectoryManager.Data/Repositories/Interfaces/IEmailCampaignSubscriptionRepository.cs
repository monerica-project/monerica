using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailCampaignSubscriptionRepository
    {
        EmailCampaignSubscription? Get(int subscriptionId);
        IEnumerable<EmailCampaignSubscription> GetByCampaign(int campaignId);
        IEnumerable<EmailCampaignSubscription> GetByEmailSubscription(int emailSubscriptionId);
        EmailCampaignSubscription SubscribeToCampaign(int campaignId, int emailSubscriptionId);
        bool UnsubscribeFromCampaign(int campaignId, int emailSubscriptionId);
        bool IsSubscribed(int campaignId, int emailSubscriptionId);
        int TotalSubscriptionsForCampaign(int campaignId);
        IEnumerable<EmailSubscription> GetActiveSubscribers(int campaignId);
        bool HasReceivedMessage(int emailSubscriptionId, int emailMessageId);
    }
}