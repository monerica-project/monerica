using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Emails;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace DirectoryManager.Web.Controllers
{
    public class EmailSubscriptionController : Controller
    {
        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;
        private readonly IBlockedIPRepository blockedIPRepository;
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;
        private readonly IEmailService emailService;
        private readonly ICacheService cacheService;

        public EmailSubscriptionController(
            IEmailSubscriptionRepository emailSubscriptionRepository,
            IBlockedIPRepository blockedIPRepository,
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            IEmailCampaignMessageRepository emailCampaignMessageRepository,
            ISentEmailRecordRepository sentEmailRecordRepository,
            IEmailService emailService,
            ICacheService cacheService)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
            this.blockedIPRepository = blockedIPRepository;
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.emailCampaignMessageRepository = emailCampaignMessageRepository;
            this.sentEmailRecordRepository = sentEmailRecordRepository;
            this.emailService = emailService;
            this.cacheService = cacheService;
        }

        [Route("EmailSubscription/CaptchaImage")]
        [HttpGet]
        public IActionResult CaptchaImage()
        {
            string captchaText = this.GenerateCaptchaText();
            this.HttpContext.Session.SetString("CaptchaCode", captchaText);

            int width = 120;
            int height = 40;

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            float fontSize = 20;

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
            };

            using var font = new SKFont
            {
                Typeface = typeface,
                Size = fontSize
            };

            // Measure text width using glyphs
            var glyphs = font.GetGlyphs(captchaText);
            var widths = font.GetGlyphWidths(glyphs);
            float textWidth = widths.Sum();

            // Measure height using font metrics
            var metrics = font.Metrics;
            float textHeight = metrics.Descent - metrics.Ascent;

            // Calculate position
            float x = (width - textWidth) / 2; // center horizontally
            float y = ((height + textHeight) / 2) - metrics.Descent; // center vertically

            // Draw the text
            canvas.DrawText(
                   text: captchaText,
                   x: x,
                   y: y,
                   textAlign: SKTextAlign.Left,
                   font: font,
                   paint: paint);

            // Encode to PNG
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return this.File(data.ToArray(), "image/png");
        }

        // Existing GET action for Subscribe
        [Route("newsletter")]
        [Route("subscribe")]
        [HttpGet]
        public IActionResult Subscribe()
        {
            this.ViewBag.NewsletterSummaryHtml = this.cacheService.GetSnippet(Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);
            return this.View();
        }

        // Modified POST action to validate CAPTCHA
        [Route("newsletter")]
        [Route("subscribe")]
        [HttpPost]
        public async Task<IActionResult> Subscribe(EmailSubscribeModel model)
        {
            // Validate CAPTCHA
            var sessionCaptcha = this.HttpContext.Session.GetString("CaptchaCode");
            if (string.IsNullOrWhiteSpace(model.Captcha) ||
                !string.Equals(model.Captcha.Trim(), sessionCaptcha, StringComparison.OrdinalIgnoreCase))
            {
                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
                this.ViewBag.NewsletterSummaryHtml = this.cacheService.GetSnippet(Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);
                return this.View(model);
            }

            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            if (!this.ModelState.IsValid ||
                !ValidationHelper.IsValidEmail(model.Email) ||
                this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.BadRequest("Invalid email");
            }

            var email = model.Email.Trim();
            var emailDbModel = this.emailSubscriptionRepository.Get(email);

            if (emailDbModel == null || emailDbModel.EmailSubscriptionId == 0)
            {
                var emailSubscription = this.emailSubscriptionRepository.Create(new EmailSubscription()
                {
                    Email = email,
                    IsSubscribed = true,
                    IpAddress = this.HttpContext.GetRemoteIpIfEnabled()
                });

                var campaigns = this.emailCampaignRepository.GetAll(0, int.MaxValue, out _);

                if (campaigns != null)
                {
                    foreach (var campaign in campaigns)
                    {
                        this.emailCampaignSubscriptionRepository.SubscribeToCampaign(
                            campaign.EmailCampaignId,
                            emailSubscription.EmailSubscriptionId);
                    }

                    var emailCampaign = this.emailCampaignRepository.GetDefault();

                    if (emailCampaign != null)
                    {
                        var subscribedDate = emailSubscription.CreateDate;

                        var sentMessageIds = this.sentEmailRecordRepository
                            .GetBySubscriptionId(emailSubscription.EmailSubscriptionId)
                            .Select(record => record.EmailMessageId)
                            .ToList();

                        var nextMessage = this.emailCampaignMessageRepository
                            .GetMessagesByCampaign(emailCampaign.EmailCampaignId)
                            .Where(m =>
                                !sentMessageIds.Contains(m.EmailMessageId) &&
                                (emailCampaign.SendMessagesPriorToSubscription || m.CreateDate >= subscribedDate))
                            .OrderBy(m => m.SequenceOrder)
                            .FirstOrDefault();

                        if (nextMessage != null)
                        {
                            var subject = nextMessage.EmailMessage.EmailSubject;
                            var plainTextContent = nextMessage.EmailMessage.EmailBodyText;
                            var htmlContent = nextMessage.EmailMessage.EmailBodyHtml;

                            // Wrap the email in a list as the method expects a List<string>
                            var recipients = new List<string> { emailSubscription.Email };

                            await this.emailService.SendEmailAsync(subject, plainTextContent, htmlContent, recipients);

                            this.sentEmailRecordRepository.LogMessageDelivery(
                                emailSubscription.EmailSubscriptionId,
                                nextMessage.EmailMessageId);
                        }
                    }
                }
            }

            return this.View("ConfirmSubscribed");
        }

        [Route("unsubscribe")]
        [AcceptVerbs("GET", "POST")]
        public IActionResult Unsubscribe([FromQuery] string? email, [FromForm] string? formEmail)
        {
            // Determine the email value from query string or form input
            var finalEmail = !string.IsNullOrWhiteSpace(email) ? email : formEmail;

            if (string.IsNullOrWhiteSpace(finalEmail))
            {
                // If no email provided, render the form for user input
                return this.View(nameof(this.Unsubscribe), null);
            }

            formEmail = formEmail?.Trim();

            var subscription = this.emailSubscriptionRepository.Get(finalEmail);
            if (subscription == null)
            {
                // Add an error if email is not found and return the view with the form
                this.ModelState.AddModelError(string.Empty, "Email not found in our subscription list.");
                return this.View(nameof(this.Unsubscribe), null);
            }

            // Mark as unsubscribed
            subscription.IsSubscribed = false;
            this.emailSubscriptionRepository.Update(subscription);

            // Pass the email to the view for confirmation message
            return this.View(nameof(this.Unsubscribe), finalEmail);
        }

        // Helper method to generate a random CAPTCHA string
        private string GenerateCaptchaText(int length = 5)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}