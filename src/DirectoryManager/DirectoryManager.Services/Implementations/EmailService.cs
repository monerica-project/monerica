using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Constants;
using DirectoryManager.Services.Exceptions;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DirectoryManager.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly SendGridClient client;
        private readonly string senderEmail;
        private readonly string senderName;
        private readonly EmailSettings emailSettings;
        private readonly IServiceScopeFactory? scopeFactory;
        private readonly string sourceApplication;
        private readonly bool useUnsubscribeHeader = false;
        private readonly string unsubscribeEmail = string.Empty;
        private readonly string unsubscribeUrlFormat = string.Empty;

        public EmailService(
            SendGridConfig config,
            EmailSettings emailSettings,
            IServiceScopeFactory? scopeFactory = null,
            string sourceApplication = EmailSendSource.Unknown)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new ArgumentException("SendGrid API Key is missing in configuration");
            }

            this.client = new SendGridClient(config.ApiKey);
            this.senderEmail = config.SenderEmail;
            this.senderName = config.SenderName;
            this.emailSettings = emailSettings;
            this.scopeFactory = scopeFactory;
            this.sourceApplication = string.IsNullOrWhiteSpace(sourceApplication)
                ? EmailSendSource.Unknown
                : sourceApplication;

            if (this.emailSettings != null &&
                !string.IsNullOrWhiteSpace(this.emailSettings.UnsubscribeEmail) &&
                !string.IsNullOrWhiteSpace(this.emailSettings.UnsubscribeUrlFormat))
            {
                this.unsubscribeEmail = this.emailSettings.UnsubscribeEmail.Trim();
                this.unsubscribeUrlFormat = this.emailSettings.UnsubscribeUrlFormat.Trim();
                this.useUnsubscribeHeader = true;
            }
        }

        public async Task SendEmailAsync(string subject, string plainTextContent, string htmlContent, List<string> recipients)
        {
            if (recipients == null || recipients.Count == 0)
            {
                throw new ArgumentException("Recipient list cannot be empty", nameof(recipients));
            }

            var from = new EmailAddress(this.senderEmail, this.senderName);
            var msg = new SendGridMessage
            {
                From = from,
                Subject = subject,
                PlainTextContent = plainTextContent,
                HtmlContent = htmlContent
            };

            if (recipients.Count == 1)
            {
                var recipient = recipients[0];
                msg.AddTo(new EmailAddress(recipient));
                this.AddUnsubscribeHeaders(recipient, msg);
            }
            else
            {
                var emailAddresses = recipients.Select(recipient => new EmailAddress(recipient)).ToList();
                msg.AddBccs(emailAddresses);
            }

            var primaryRecipient = recipients[0];
            int? statusCode = null;

            try
            {
                var response = await this.client.SendEmailAsync(msg);
                statusCode = (int)response.StatusCode;
                var body = await response.Body.ReadAsStringAsync();

                // SendGrid returns 202 Accepted on success. Anything outside 2xx is a failure
                // that the SDK does NOT throw for. We must surface it ourselves so the
                // caller can decide whether to retry or skip logging the delivery.
                if (statusCode < 200 || statusCode >= 300)
                {
                    this.LogSend(primaryRecipient, recipients.Count, subject, false, statusCode, body);
                    throw new SendGridDeliveryException(statusCode.Value, body);
                }

                this.LogSend(primaryRecipient, recipients.Count, subject, true, statusCode, null);
                Console.WriteLine($"SendGrid accepted message ({statusCode}) for {string.Join(",", recipients)}");
            }
            catch (SendGridDeliveryException)
            {
                // Already logged as a failure above; let the caller's retry logic handle it.
                throw;
            }
            catch (Exception ex)
            {
                // Network or SDK-level failure before a response was received.
                this.LogSend(primaryRecipient, recipients.Count, subject, false, statusCode, ex.Message);
                throw;
            }
        }

        public void AddUnsubscribeHeaders(string recipientEmail, SendGridMessage msg)
        {
            if (!this.useUnsubscribeHeader)
            {
                return;
            }

            var unsubscribeUrl = string.Format(this.unsubscribeUrlFormat, Uri.EscapeDataString(recipientEmail));
            msg.AddHeader("List-Unsubscribe", $"<mailto:{this.unsubscribeEmail}>, <{unsubscribeUrl}>");
            msg.AddHeader("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        /// <summary>
        /// Writes one row to the generic EmailSendLog audit table. Best-effort:
        /// a logging failure must NEVER interfere with sending, so all exceptions
        /// here are swallowed (and surfaced to the console only). The scoped log
        /// repository is resolved through a fresh DI scope per send so that a
        /// singleton EmailService (the console jobs) never captures a DbContext.
        /// </summary>
        private void LogSend(
            string recipientEmail,
            int recipientCount,
            string subject,
            bool isSuccess,
            int? statusCode,
            string? errorMessage)
        {
            if (this.scopeFactory == null)
            {
                return;
            }

            try
            {
                using var scope = this.scopeFactory.CreateScope();
                var logRepo = scope.ServiceProvider.GetRequiredService<IEmailSendLogRepository>();
                logRepo.Log(
                    this.sourceApplication,
                    recipientEmail,
                    recipientCount,
                    subject,
                    isSuccess,
                    statusCode,
                    errorMessage);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"WARNING: failed to write EmailSendLog row: {logEx.Message}");
            }
        }
    }
}
