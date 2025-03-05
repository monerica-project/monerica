using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Services.Interfaces;
using DirectoryManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.NewsletterSender.Services.Implementations
{
    /// <summary>
    /// Service for processing email campaigns.
    /// </summary>
    public class EmailCampaignProcessingService : IEmailCampaignProcessingService
    {
        private const int DefaultDelayMilliseconds = 250;
        private readonly int delayMilliseconds;
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;
        private readonly IEmailService emailService;
        private readonly IContentSnippetRepository contentSnippetRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailCampaignProcessingService"/> class.
        /// </summary>
        public EmailCampaignProcessingService(
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            IEmailCampaignMessageRepository emailCampaignMessageRepository,
            ISentEmailRecordRepository sentEmailRecordRepository,
            IEmailService emailService,
            IContentSnippetRepository contentSnippetRepository,
            IConfiguration configuration)
        {
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.emailCampaignMessageRepository = emailCampaignMessageRepository;
            this.sentEmailRecordRepository = sentEmailRecordRepository;
            this.emailService = emailService;
            this.contentSnippetRepository = contentSnippetRepository;
            this.delayMilliseconds = configuration.GetValue<int>("EmailCampaignProcessing:DelayMilliseconds", DefaultDelayMilliseconds);
        }

        public async Task ProcessCampaignsAsync()
        {
            var unsubscribeText = this.contentSnippetRepository.GetValue(Data.Enums.SiteConfigSetting.EmailSettingUnsubscribeFooterText);
            var unsubscribeHtml = this.contentSnippetRepository.GetValue(Data.Enums.SiteConfigSetting.EmailSettingUnsubscribeFooterHtml);
            var campaigns = this.emailCampaignRepository.GetAll(0, int.MaxValue, out _);

            foreach (var campaign in campaigns)
            {
                // Respect campaign start date
                if (campaign.StartDate.HasValue && campaign.StartDate.Value > DateTime.UtcNow)
                {
                    Console.WriteLine($"Skipping campaign '{campaign.Name}' as it hasn't started yet.");
                    continue;
                }

                if (!campaign.IsEnabled)
                {
                    Console.WriteLine($"Skipping campaign '{campaign.Name}' as isn't enabled.");
                    continue;
                }

                var subscribers = this.emailCampaignSubscriptionRepository.GetSubscribersByCampaign(campaign.EmailCampaignId);

                foreach (var subscription in subscribers)
                {
                    var subscribedDate = subscription.SubscribedDate;

                    // Get all sent messages for this subscriber
                    var sentMessageIds = this.sentEmailRecordRepository
                        .GetBySubscriptionId(subscription.EmailSubscriptionId)
                        .Select(record => record.EmailMessageId)
                        .ToList();

                    // Get the next eligible message (based on SubscribedDate and CreatedDate)
                    var nextMessage = this.emailCampaignMessageRepository
                        .GetMessagesByCampaign(campaign.EmailCampaignId)
                        .Where(m =>
                            !sentMessageIds.Contains(m.EmailMessageId) && // Not sent already
                            (campaign.SendMessagesPriorToSubscription || m.CreateDate >= subscribedDate)) // Logic for new behavior
                        .OrderBy(m => m.SequenceOrder)
                        .FirstOrDefault();

                    if (nextMessage == null)
                    {
                        Console.WriteLine($"No eligible messages for subscriber {subscription.EmailSubscription.Email} in campaign '{campaign.Name}'.");
                        continue;
                    }

                    // Ensure interval days have passed since last sent email
                    var lastSentMessage = this.sentEmailRecordRepository
                        .GetBySubscriptionId(subscription.EmailSubscriptionId)
                        .OrderByDescending(record => record.SentDate)
                        .FirstOrDefault();

                    if (lastSentMessage != null &&
                        (DateTime.UtcNow - lastSentMessage.SentDate).TotalDays < campaign.IntervalDays)
                    {
                        Console.WriteLine($"Skipping subscriber {subscription.EmailSubscription.Email} as interval days have not elapsed.");
                        continue;
                    }

                    // Prepare email content
                    var subject = nextMessage.EmailMessage.EmailSubject;
                    var plainTextContent = this.AppendFooter(nextMessage.EmailMessage.EmailBodyText, unsubscribeText);
                    var htmlContent = this.AppendFooter(nextMessage.EmailMessage.EmailBodyHtml, unsubscribeHtml);

                    // Send the email
                    await this.SendEmailAsync(subject, plainTextContent, htmlContent, subscription.EmailSubscription.Email);
                    await Task.Delay(this.delayMilliseconds); // Add delay to respect SendGrid's rate limits

                    // Log message delivery
                    this.sentEmailRecordRepository.LogMessageDelivery(
                        subscription.EmailSubscriptionId,
                        nextMessage.EmailMessageId);

                    Console.WriteLine($"Email sent to {subscription.EmailSubscription.Email} for campaign '{campaign.Name}'.");
                }
            }
        }

        private string AppendFooter(string body, string footer)
        {
            if (string.IsNullOrWhiteSpace(footer))
            {
                return body;
            }

            var replacedText = body.Replace(Common.Constants.StringConstants.UnsubscribeToken, footer);

            return replacedText;
        }

        /// <summary>
        /// Sends an email message using the EmailService.
        /// </summary>
        private async Task SendEmailAsync(string subject, string plainTextContent, string htmlContent, string recipientEmail)
        {
            var recipients = new List<string> { recipientEmail };

            try
            {
                await this.emailService.SendEmailAsync(subject, plainTextContent, htmlContent, recipients);
                Console.WriteLine($"Email sent to {recipientEmail} with subject: {subject}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email to {recipientEmail}: {ex.Message}");
            }
        }
    }
}