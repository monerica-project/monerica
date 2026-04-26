using DirectoryManager.Services.Exceptions;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
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
        private readonly bool useUnsubscribeHeader = false;
        private readonly string unsubscribeEmail = string.Empty;
        private readonly string unsubscribeUrlFormat = string.Empty;

        public EmailService(SendGridConfig config, EmailSettings emailSettings)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new ArgumentException("SendGrid API Key is missing in configuration");
            }

            this.client = new SendGridClient(config.ApiKey);
            this.senderEmail = config.SenderEmail;
            this.senderName = config.SenderName;
            this.emailSettings = emailSettings;

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

            var response = await this.client.SendEmailAsync(msg);
            var statusCode = (int)response.StatusCode;
            var body = await response.Body.ReadAsStringAsync();

            // SendGrid returns 202 Accepted on success. Anything outside 2xx is a failure
            // that the SDK does NOT throw for. We must surface it ourselves so the
            // caller can decide whether to retry or skip logging the delivery.
            if (statusCode < 200 || statusCode >= 300)
            {
                throw new SendGridDeliveryException(statusCode, body);
            }

            Console.WriteLine($"SendGrid accepted message ({statusCode}) for {string.Join(",", recipients)}");
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
    }
}