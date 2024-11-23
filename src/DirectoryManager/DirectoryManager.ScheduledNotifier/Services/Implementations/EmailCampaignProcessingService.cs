using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.ScheduledNotifier.Services.Interfaces;

namespace DirectoryManager.ScheduledNotifier.Services.Implementations
{
    public class EmailCampaignProcessingService : IEmailCampaignProcessingService
    {
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;

        public EmailCampaignProcessingService(
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            ISentEmailRecordRepository sentEmailRecordRepository)
        {
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.sentEmailRecordRepository = sentEmailRecordRepository;
        }

        public void ProcessCampaign(int campaignId)
        {
            // Retrieve ordered messages for the specified campaign
            var messages = this.emailCampaignRepository.GetOrderedMessages(campaignId);

            // Get active subscribers to the campaign
            var subscribers = this.emailCampaignSubscriptionRepository.GetActiveSubscribers(campaignId);

            foreach (var message in messages)
            {
                foreach (var subscriber in subscribers)
                {
                    // Check if the subscriber has already received this message
                    if (!this.emailCampaignSubscriptionRepository.HasReceivedMessage(subscriber.EmailSubscriptionId, message.EmailMessageId))
                    {
                        // Here you would have logic to send the message (e.g., via email service)
                        // For demonstration, we are assuming sending is handled outside this service

                        // Log the delivery of the message
                        this.sentEmailRecordRepository.LogMessageDelivery(subscriber.EmailSubscriptionId, message.EmailMessageId);
                    }
                }
            }
        }
    }
}