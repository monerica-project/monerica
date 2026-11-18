using DirectoryManager.Data.Models.Emails;
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
        private readonly UnsubscribeTokenSigner unsubscribeSigner;

        public EmailService(
            SendGridConfig config,
            EmailSettings emailSettings,
            IServiceScopeFactory? scopeFactory = null,
            string sourceApplication = EmailSendSource.Unknown,
            HttpClient? httpClient = null)
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new ArgumentException("SendGrid API Key is missing in configuration");
            }

            // httpClient: tests only (a fake handler instead of the network).
            this.client = httpClient == null
                ? new SendGridClient(config.ApiKey)
                : new SendGridClient(httpClient, config.ApiKey);
            this.unsubscribeSigner = new UnsubscribeTokenSigner(config.ApiKey);
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
            var content = BuildContentSnapshot(msg, plainTextContent, htmlContent, recipients);

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
                    this.LogSend(primaryRecipient, recipients.Count, subject, false, statusCode, body, content);
                    throw new SendGridDeliveryException(statusCode.Value, body);
                }

                this.LogSend(primaryRecipient, recipients.Count, subject, true, statusCode, null, content);
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
                this.LogSend(primaryRecipient, recipients.Count, subject, false, statusCode, ex.Message, content);
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
            var oneClickUrl = BuildOneClickUnsubscribeUrl(unsubscribeUrl, recipientEmail, this.unsubscribeSigner);

            if (oneClickUrl == null)
            {
                // No signed link possible: point at the (captcha) form only, and do NOT claim
                // RFC 8058 one-click support, since a POST to that form would fail.
                msg.AddHeader("List-Unsubscribe", $"<mailto:{this.unsubscribeEmail}>, <{unsubscribeUrl}>");
                return;
            }

            // RFC 8058: exactly one HTTPS URI, which takes a cookie-less POST of
            // "List-Unsubscribe=One-Click" and unsubscribes this recipient.
            msg.AddHeader("List-Unsubscribe", $"<mailto:{this.unsubscribeEmail}>, <{oneClickUrl}>");
            msg.AddHeader("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        /// <summary>
        /// The one-click link lives on the same host as the configured unsubscribe page
        /// (EmailSettingUnsubscribeUrlFormat), at /unsubscribe/one-click, with the address and its
        /// signed token in the query. Returns null when no token can be made or the configured
        /// page is not an absolute HTTPS URL.
        /// </summary>
        /// <returns>The one-click URL, or null.</returns>
        public static string? BuildOneClickUnsubscribeUrl(string unsubscribeUrl, string recipientEmail, UnsubscribeTokenSigner signer)
        {
            var token = signer.CreateToken(recipientEmail);
            if (token == null ||
                !Uri.TryCreate(unsubscribeUrl, UriKind.Absolute, out var page) ||
                page.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return $"{page.GetLeftPart(UriPartial.Authority)}/unsubscribe/one-click" +
                   $"?email={Uri.EscapeDataString(recipientEmail.Trim())}&token={token}";
        }

        /// <summary>
        /// The stored copy of what was sent (redacted; see <see cref="EmailContentSnapshot"/>). Never
        /// throws: if it can't be built the send goes ahead and the log row just has no content.
        /// </summary>
        private static EmailSendLogContent? BuildContentSnapshot(SendGridMessage msg, string? plainText, string? html, List<string> recipients)
        {
            try
            {
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (msg.Headers != null)
                {
                    foreach (var h in msg.Headers)
                    {
                        headers[h.Key] = h.Value;
                    }
                }

                var personalHeaders = msg.Personalizations?.FirstOrDefault()?.Headers;
                if (personalHeaders != null)
                {
                    foreach (var h in personalHeaders)
                    {
                        headers[h.Key] = h.Value;
                    }
                }

                return EmailContentSnapshot.Build(
                    msg.From?.Email ?? string.Empty,
                    msg.From?.Name,
                    msg.ReplyTo?.Email,
                    headers,
                    plainText,
                    html,
                    recipients);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: could not prepare email content for the send log: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes one row to the generic EmailSendLog audit table, and the message content (once per
        /// distinct content) when given. Best-effort: a logging failure must NEVER interfere with
        /// sending, so all exceptions here are swallowed (and surfaced to the console only); a content
        /// storage failure still writes the log row (without content). Each write gets a fresh DI scope,
        /// so a singleton EmailService (the console jobs) never captures a DbContext and a failed content
        /// insert can't poison the log row's DbContext.
        /// </summary>
        private void LogSend(
            string recipientEmail,
            int recipientCount,
            string subject,
            bool isSuccess,
            int? statusCode,
            string? errorMessage,
            EmailSendLogContent? content = null)
        {
            if (this.scopeFactory == null)
            {
                return;
            }

            int? contentId = null;
            if (content != null)
            {
                try
                {
                    using var contentScope = this.scopeFactory.CreateScope();
                    var contentRepo = contentScope.ServiceProvider.GetRequiredService<IEmailSendLogContentRepository>();
                    contentId = contentRepo.GetOrCreate(content);
                }
                catch (Exception contentEx)
                {
                    Console.WriteLine($"WARNING: failed to store email content for the send log: {contentEx.Message}");
                }
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
                    errorMessage,
                    contentId);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"WARNING: failed to write EmailSendLog row: {logEx.Message}");
            }
        }
    }
}
