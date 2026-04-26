using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Services.Interfaces;
using DirectoryManager.Services.Exceptions;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.NewsletterSender.Services.Implementations
{
    public class EmailCampaignProcessingService : IEmailCampaignProcessingService
    {
        private const int DefaultDelayMilliseconds = 250;
        private const int MaxSendAttempts = 3;
        private readonly int delayMilliseconds;
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;
        private readonly IEmailService emailService;
        private readonly IContentSnippetRepository contentSnippetRepository;

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

            int totalSent = 0;
            int totalFailed = 0;

            foreach (var campaign in campaigns)
            {
                if (campaign.StartDate.HasValue && campaign.StartDate.Value > DateTime.UtcNow)
                {
                    continue;
                }

                if (!campaign.IsEnabled)
                {
                    continue;
                }

                var messages = this.emailCampaignMessageRepository
                                  .GetMessagesByCampaign(campaign.EmailCampaignId)
                                  .OrderBy(m => m.SequenceOrder)
                                  .ToList();

                var messageCache = new Dictionary<int, (string PlainText, string Html)>(messages.Count);
                foreach (var msg in messages)
                {
                    messageCache[msg.EmailMessageId] = (
                        PlainText: this.AppendFooter(msg.EmailMessage.EmailBodyText, unsubscribeText),
                        Html: this.AppendFooter(msg.EmailMessage.EmailBodyHtml, unsubscribeHtml));
                }

                var subscribers = this.emailCampaignSubscriptionRepository.GetSubscribersByCampaign(campaign.EmailCampaignId);
                foreach (var subscription in subscribers)
                {
                    var subscribedDate = subscription.SubscribedDate;

                    // Single DB call; reuse for both sentIds and last-sent check.
                    var sentRecords = this.sentEmailRecordRepository
                                          .GetBySubscriptionId(subscription.EmailSubscriptionId)
                                          .ToList();
                    var sentIds = sentRecords.Select(r => r.EmailMessageId).ToHashSet();

                    var next = messages
                        .FirstOrDefault(m => !sentIds.Contains(m.EmailMessageId)
                                             && (campaign.SendMessagesPriorToSubscription || m.CreateDate >= subscribedDate));
                    if (next == null)
                    {
                        continue;
                    }

                    var last = sentRecords.OrderByDescending(r => r.SentDate).FirstOrDefault();
                    if (last != null && (DateTime.UtcNow - last.SentDate).TotalDays < campaign.IntervalDays)
                    {
                        continue;
                    }

                    var (plainTextContent, htmlContent) = messageCache[next.EmailMessageId];
                    var subject = next.EmailMessage.EmailSubject;
                    var recipientEmail = subscription.EmailSubscription.Email;

                    var delivered = await this.TrySendWithRetryAsync(
                        subject, plainTextContent, htmlContent, recipientEmail);

                    if (delivered)
                    {
                        this.sentEmailRecordRepository.LogMessageDelivery(
                            subscription.EmailSubscriptionId,
                            next.EmailMessageId);
                        totalSent++;
                    }
                    else
                    {
                        totalFailed++;
                        Console.WriteLine($"GIVING UP on {recipientEmail} for message {next.EmailMessageId}. Will retry next run.");
                    }

                    await Task.Delay(this.delayMilliseconds);
                }
            }

            Console.WriteLine($"Run summary: {totalSent} sent, {totalFailed} failed (will retry next run).");
        }

        private async Task<bool> TrySendWithRetryAsync(
            string subject, string plainText, string html, string recipient)
        {
            var recipients = new List<string> { recipient };

            for (int attempt = 1; attempt <= MaxSendAttempts; attempt++)
            {
                try
                {
                    await this.emailService.SendEmailAsync(subject, plainText, html, recipients);
                    Console.WriteLine($"Sent to {recipient} (attempt {attempt}): {subject}");
                    return true;
                }
                catch (SendGridDeliveryException ex) when (ex.IsTransient && attempt < MaxSendAttempts)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Transient SendGrid error for {recipient} (attempt {attempt}/{MaxSendAttempts}, status {ex.StatusCode}). Backing off {backoff.TotalSeconds}s.");
                    await Task.Delay(backoff);
                }
                catch (SendGridDeliveryException ex)
                {
                    // Non-transient (4xx other than 429) — no point retrying this run.
                    Console.WriteLine($"Permanent SendGrid error for {recipient} (status {ex.StatusCode}): {ex.ResponseBody}");
                    return false;
                }
                catch (Exception ex) when (attempt < MaxSendAttempts)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Unexpected error for {recipient} (attempt {attempt}/{MaxSendAttempts}): {ex.Message}. Backing off {backoff.TotalSeconds}s.");
                    await Task.Delay(backoff);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Final unexpected error for {recipient}: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        private string AppendFooter(string body, string footer)
        {
            if (string.IsNullOrWhiteSpace(footer))
            {
                return body;
            }

            return body.Replace(Common.Constants.StringConstants.UnsubscribeToken, footer);
        }
    }
}