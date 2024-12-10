using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Services.Interfaces;
using DirectoryManager.Services.Interfaces;

namespace DirectoryManager.NewsletterSender.Services.Implementations
{
    /// <summary>
    /// Service for processing email campaigns.
    /// </summary>
    public class EmailCampaignProcessingService : IEmailCampaignProcessingService
    {
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;
        private readonly IEmailService emailService;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailCampaignProcessingService"/> class.
        /// </summary>
        public EmailCampaignProcessingService(
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            IEmailCampaignMessageRepository emailCampaignMessageRepository,
            ISentEmailRecordRepository sentEmailRecordRepository,
            IEmailService emailService)
        {
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.emailCampaignMessageRepository = emailCampaignMessageRepository;
            this.sentEmailRecordRepository = sentEmailRecordRepository;
            this.emailService = emailService;
        }

        /// <summary>
        /// Processes active email campaigns and sends the next unsent message.
        /// </summary>
        /// <returns>Representing the asynchronous operation.</returns>
        public async Task ProcessCampaignsAsync()
        {
            var campaigns = this.emailCampaignRepository.GetAll(0, int.MaxValue, out _);

            foreach (var campaign in campaigns)
            {
                // Ensure campaign start date is respected
                if (campaign.StartDate.HasValue && campaign.StartDate.Value > DateTime.UtcNow)
                {
                    Console.WriteLine($"Skipping campaign '{campaign.Name}' as it hasn't started yet.");
                    continue; // Skip campaigns that haven't reached their start date
                }

                var subscribers = this.emailCampaignSubscriptionRepository.GetActiveSubscribers(campaign.EmailCampaignId);

                foreach (var subscriber in subscribers)
                {
                    if (subscriber.Email != "admin@bootbaron.com")
                    {
                        continue;
                    }

                    // Get all sent messages for this subscriber
                    var sentMessages = this.sentEmailRecordRepository.GetBySubscriptionId(subscriber.EmailSubscriptionId)
                        .OrderByDescending(record => record.SentDate)
                        .ToList();

                    // Check the last sent message's date
                    if (sentMessages.Any())
                    {
                        var lastSentDate = sentMessages.First().SentDate;

                        // Ensure interval days have passed
                        if ((DateTime.UtcNow - lastSentDate).TotalDays < campaign.IntervalDays)
                        {
                            Console.WriteLine($"Skipping subscriber {subscriber.Email} as interval days have not elapsed.");
                            continue;
                        }
                    }

                    // Get the next unsent message
                    var sentMessageIds = sentMessages.Select(record => record.EmailMessageId).ToList();
                    var nextMessage = this.emailCampaignMessageRepository
                        .GetMessagesByCampaign(campaign.EmailCampaignId)
                        .FirstOrDefault(message => !sentMessageIds.Contains(message.EmailMessage.EmailMessageId));

                    if (nextMessage == null)
                    {
                        Console.WriteLine($"All messages have been sent to subscriber {subscriber.Email}.");
                        continue; // All messages for this campaign have been sent
                    }

                    // Prepare email content
                    var subject = nextMessage.EmailMessage.EmailSubject;
                    var plainTextContent = nextMessage.EmailMessage.EmailBodyText;
                    var htmlContent = nextMessage.EmailMessage.EmailBodyHtml;

                    // Send the email
                    await this.SendEmailAsync(subject, plainTextContent, htmlContent, subscriber.Email);

                    // Log the message delivery
                    this.sentEmailRecordRepository.LogMessageDelivery(
                        subscriber.EmailSubscriptionId,
                        nextMessage.EmailMessage.EmailMessageId);

                    Console.WriteLine($"Email sent to {subscriber.Email} for campaign '{campaign.Name}'.");
                }
            }
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