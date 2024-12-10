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

        public EmailService(SendGridConfig config)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new ArgumentException("SendGrid API Key is missing in configuration");
            }

            this.client = new SendGridClient(config.ApiKey);
            this.senderEmail = config.SenderEmail;
            this.senderName = config.SenderName;
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

            msg.AddHeader("List-Unsubscribe", "<mailto:unsubscribe@yourdomain.com>, <https://yourdomain.com/unsubscribe?email=recipient@example.com>");

            if (recipients.Count == 1)
            {
                msg.AddTo(new EmailAddress(recipients[0]));
            }
            else
            {
                var emailAddresses = new List<EmailAddress>();
                foreach (var recipient in recipients)
                {
                    emailAddresses.Add(new EmailAddress(recipient));
                }

                msg.AddBccs(emailAddresses);
            }

            try
            {
                var response = await this.client.SendEmailAsync(msg);

                // Optionally, log or handle response here
                Console.WriteLine(response.StatusCode);
                Console.WriteLine(await response.Body.ReadAsStringAsync());
                Console.WriteLine(response.Headers);
            }
            catch
            {
            }
        }
    }
}