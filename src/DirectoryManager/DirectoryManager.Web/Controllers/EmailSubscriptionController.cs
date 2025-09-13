using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Emails;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

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

        // GET /newsletter (or /subscribe)
        [Route("newsletter")]
        [Route("subscribe")]
        [HttpGet]
        public async Task<IActionResult> SubscribeAsync()
        {
            this.ViewBag.NewsletterSummaryHtml =
                await this.cacheService.GetSnippetAsync(DirectoryManager.Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);

            // supply an empty model so ValidationMessage/ValidationSummary can render cleanly
            return this.View(new EmailSubscribeModel());
        }

        // POST /newsletter (or /subscribe)
        [Route("newsletter")]
        [Route("subscribe")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(EmailSubscribeModel model)
        {
            // Make sure page content always repopulates when we re-render on error
            this.ViewBag.NewsletterSummaryHtml =
                await this.cacheService.GetSnippetAsync(DirectoryManager.Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);

            // Determine captcha context key from the form (hidden field in _CaptchaBlock)
            var ctx = (this.Request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ctx))
            {
                ctx = "newsletter";
            }

            // Validate CAPTCHA (consumes stored value so next render shows a fresh one)
            var captchaOk = CaptchaTools.Validate(this.HttpContext, ctx, model.Captcha, consume: true);
            if (!captchaOk)
            {
                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
            }

            // Validate email format
            if (string.IsNullOrWhiteSpace(model.Email) || !ValidationHelper.IsValidEmail(model.Email))
            {
                this.ModelState.AddModelError("Email", "Please enter a valid email address.");
            }

            // Blocked IP check (add as a model-level error so it appears in the summary)
            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                this.ModelState.AddModelError(string.Empty, "Your IP is not allowed to subscribe.");
            }

            // If any errors were added above, re-render the same view with messages
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            // --- Proceed with subscription workflow ---
            var email = model.Email!.Trim();
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
                this.ModelState.AddModelError(string.Empty, "Email not found in our subscription list.");
                return this.View(nameof(this.Unsubscribe), null);
            }

            // Mark as unsubscribed
            subscription.IsSubscribed = false;
            this.emailSubscriptionRepository.Update(subscription);

            // Pass the email to the view for confirmation message
            return this.View(nameof(this.Unsubscribe), finalEmail);
        }
    }
}
