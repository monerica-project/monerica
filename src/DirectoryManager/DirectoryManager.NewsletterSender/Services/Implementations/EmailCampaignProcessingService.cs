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
                    continue;
                }

                if (!campaign.IsEnabled)
                {
                    continue;
                }

                // Pre-cache all messages for this campaign, with footers appended
                var messages = this.emailCampaignMessageRepository
                                  .GetMessagesByCampaign(campaign.EmailCampaignId)
                                  .OrderBy(m => m.SequenceOrder)
                                  .ToList();

                var messageCache = new Dictionary<int, (string PlainText, string Html)>(messages.Count);
                foreach (var msg in messages)
                {
                    var id = msg.EmailMessageId;
                    messageCache[id] = (
                        PlainText: this.AppendFooter(msg.EmailMessage.EmailBodyText, unsubscribeText),
                        Html: this.AppendFooter(msg.EmailMessage.EmailBodyHtml, unsubscribeHtml));
                }

                var subscribers = this.emailCampaignSubscriptionRepository.GetSubscribersByCampaign(campaign.EmailCampaignId);
                foreach (var subscription in subscribers)
                {
                    var subscribedDate = subscription.SubscribedDate;

                    // Which messages have already gone out?
                    var sentIds = this.sentEmailRecordRepository
                                  .GetBySubscriptionId(subscription.EmailSubscriptionId)
                                  .Select(r => r.EmailMessageId)
                                  .ToHashSet();

                    // Pick next eligible message
                    var next = messages
                        .Where(m => !sentIds.Contains(m.EmailMessageId)
                                    && (campaign.SendMessagesPriorToSubscription || m.CreateDate >= subscribedDate))
                        .FirstOrDefault();
                    if (next == null)
                    {
                        continue;
                    }

                    // Enforce interval
                    var last = this.sentEmailRecordRepository
                               .GetBySubscriptionId(subscription.EmailSubscriptionId)
                               .OrderByDescending(r => r.SentDate)
                               .FirstOrDefault();
                    if (last != null &&
                        (DateTime.UtcNow - last.SentDate).TotalDays < campaign.IntervalDays)
                    {
                        continue;
                    }

                    // Reuse the pre-built body
                    var (plainTextContent, htmlContent) = messageCache[next.EmailMessageId];
                    var subject = next.EmailMessage.EmailSubject;

                    await this.SendEmailAsync(subject, plainTextContent, htmlContent, subscription.EmailSubscription.Email);
                    await Task.Delay(this.delayMilliseconds);

                    this.sentEmailRecordRepository.LogMessageDelivery(
                        subscription.EmailSubscriptionId,
                        next.EmailMessageId);
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