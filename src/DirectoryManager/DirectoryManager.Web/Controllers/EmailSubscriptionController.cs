using System.Net;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Emails;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class EmailSubscriptionController : Controller
    {
        /// <summary>Data-protection purpose of the confirmation token (web app only).</summary>
        public const string ConfirmTokenPurpose = "DirectoryManager.Newsletter.ConfirmSubscription.v1";

        private const string CaptchaContextUnsubscribe = "newsletter-unsubscribe";
        private const string ConfirmThrottleKeyPrefix = "newsletter-confirm-sent:";

        /// <summary>How long a confirmation link works.</summary>
        public static readonly TimeSpan ConfirmLinkLifetime = TimeSpan.FromHours(48);

        /// <summary>At most one confirmation email per address in this window (no IPs are used).</summary>
        public static readonly TimeSpan ConfirmResendWindow = TimeSpan.FromHours(1);

        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;
        private readonly IEmailService emailService;
        private readonly ICacheService cacheService;
        private readonly ITimeLimitedDataProtector confirmProtector;
        private readonly IMemoryCache memoryCache;
        private readonly UnsubscribeTokenSigner unsubscribeSigner;
        private readonly ILogger<EmailSubscriptionController> logger;

        public EmailSubscriptionController(
            IEmailSubscriptionRepository emailSubscriptionRepository,
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            IEmailCampaignMessageRepository emailCampaignMessageRepository,
            ISentEmailRecordRepository sentEmailRecordRepository,
            IEmailService emailService,
            ICacheService cacheService,
            IDataProtectionProvider dataProtectionProvider,
            IMemoryCache memoryCache,
            UnsubscribeTokenSigner unsubscribeSigner,
            ILogger<EmailSubscriptionController> logger)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.emailCampaignMessageRepository = emailCampaignMessageRepository;
            this.sentEmailRecordRepository = sentEmailRecordRepository;
            this.emailService = emailService;
            this.cacheService = cacheService;
            this.confirmProtector = dataProtectionProvider.CreateProtector(ConfirmTokenPurpose).ToTimeLimitedDataProtector();
            this.memoryCache = memoryCache;
            this.unsubscribeSigner = unsubscribeSigner;
            this.logger = logger;
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
            // Keep summary content populated for re-renders
            this.ViewBag.NewsletterSummaryHtml =
                await this.cacheService.GetSnippetAsync(DirectoryManager.Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);

            // Captcha context
            var ctx = (this.Request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ctx))
            {
                ctx = "newsletter";
            }

            // Validate CAPTCHA
            var captchaOk = CaptchaTools.Validate(this.HttpContext, ctx, model.Captcha, consume: true);
            if (!captchaOk)
            {
                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
            }

            // Validate email using the new helper
            var (okEmail, normalizedEmail, emailError) = EmailValidationHelper.Validate(model.Email);
            if (!okEmail)
            {
                this.ModelState.AddModelError("Email", emailError!);
            }
            else
            {
                model.Email = normalizedEmail!;
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            // Double opt-in: nothing is stored yet. The address only gets a link; the subscription
            // is created when its owner opens the link and presses "Confirm subscription".
            // The page below is the same whatever happens here (already subscribed, mail already
            // sent recently, sending failed), so the form reveals nothing about who subscribes.
            await this.SendConfirmationIfNeededAsync(model.Email!);

            return this.View("CheckEmail");
        }

        // GET /newsletter/confirm?token=...  (the link in the confirmation email)
        // Only shows a button: mail scanners prefetch links, so opening it must not subscribe.
        [Route("newsletter/confirm")]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Confirm([FromQuery] string? token)
        {
            var email = this.ReadConfirmToken(token);
            return this.View("Confirm", email == null ? null : new NewsletterLinkModel { Email = email, Token = token!.Trim() });
        }

        // POST /newsletter/confirm  (the "Confirm subscription" button). Safe to repeat.
        [Route("newsletter/confirm")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ConfirmPost([FromForm] string? token)
        {
            var email = this.ReadConfirmToken(token);
            if (email == null)
            {
                return this.View("Confirm", null);
            }

            await this.ActivateSubscriptionAsync(email);
            return this.View("ConfirmSubscribed");
        }

        /// <summary>Creates a signed, time-limited confirmation token for an address.</summary>
        /// <returns>The token (hex, safe in a query string).</returns>
        [NonAction]
        public string CreateConfirmToken(string email) =>
            Convert.ToHexStringLower(this.confirmProtector.Protect(Encoding.UTF8.GetBytes(email), DateTimeOffset.UtcNow.Add(ConfirmLinkLifetime)));

        // GET /unsubscribe/one-click?email=...&token=...  (the List-Unsubscribe link)
        // Shows one button. Nothing changes on GET, so link scanners can't unsubscribe anyone.
        [Route("unsubscribe/one-click")]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult OneClickUnsubscribe([FromQuery] string? email, [FromQuery] string? token)
        {
            if (!this.unsubscribeSigner.IsValid(email, token))
            {
                return this.View("OneClickUnsubscribe", null);
            }

            return this.View("OneClickUnsubscribe", new NewsletterLinkModel { Email = email!.Trim(), Token = token!.Trim() });
        }

        // POST /unsubscribe/one-click?email=...&token=...
        // RFC 8058 one-click: Gmail/Yahoo POST "List-Unsubscribe=One-Click" here with no cookies,
        // so no captcha, antiforgery token or login. The signed token is the authorization and
        // only unsubscribes the address it was made for. The answer never depends on whether the
        // address was on the list.
        [Route("unsubscribe/one-click")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult OneClickUnsubscribePost([FromQuery] string? email, [FromQuery] string? token)
        {
            if (!this.unsubscribeSigner.IsValid(email, token))
            {
                return this.View("OneClickUnsubscribe", null);
            }

            var address = email!.Trim();
            var subscription = this.emailSubscriptionRepository.Get(address);
            if (subscription != null && subscription.IsSubscribed)
            {
                subscription.IsSubscribed = false;
                this.emailSubscriptionRepository.Update(subscription);
            }

            return this.View("OneClickUnsubscribe", new NewsletterLinkModel { Email = address, Token = token!.Trim(), Done = true });
        }

        [Route("unsubscribe")]
        [AcceptVerbs("GET", "POST")]
        public IActionResult Unsubscribe([FromQuery] string? email, [FromForm] string? formEmail)
        {
            // A link (GET) only pre-fills the form. Unsubscribing itself is a POST with a captcha,
            // so nobody can unsubscribe someone else in bulk by requesting URLs.
            if (!HttpMethods.IsPost(this.Request.Method))
            {
                this.ViewBag.PrefillEmail = (email ?? string.Empty).Trim();
                return this.View(nameof(this.Unsubscribe), null);
            }

            var finalEmailRaw = formEmail;
            this.ViewBag.PrefillEmail = (finalEmailRaw ?? string.Empty).Trim();

            if (!CaptchaTools.Validate(this.HttpContext, CaptchaContextUnsubscribe, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                this.ModelState.AddModelError(string.Empty, "Incorrect CAPTCHA. Please try again.");
                return this.View(nameof(this.Unsubscribe), null);
            }

            // If nothing supplied, render the form to collect email
            if (string.IsNullOrWhiteSpace(finalEmailRaw))
            {
                return this.View(nameof(this.Unsubscribe), null);
            }

            // Validate email with helper before proceeding
            var (okEmail, normalizedEmail, emailError) = EmailValidationHelper.Validate(finalEmailRaw);
            if (!okEmail)
            {
                this.ModelState.AddModelError("Email", emailError!);
                return this.View(nameof(this.Unsubscribe), null);
            }

            var subscription = this.emailSubscriptionRepository.Get(normalizedEmail!);

            // Same answer whether or not the address is on the list, so the form cannot be used
            // to find out who subscribes.
            if (subscription != null)
            {
                subscription.IsSubscribed = false;
                this.emailSubscriptionRepository.Update(subscription);
            }

            // Pass normalized email to the view
            return this.View(nameof(this.Unsubscribe), normalizedEmail);
        }

        private static string ThrottleKey(string email) =>
            ConfirmThrottleKeyPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(UnsubscribeTokenSigner.Normalize(email))));

        private async Task SendConfirmationIfNeededAsync(string email)
        {
            // Existing subscribers are not emailed again.
            var existing = this.emailSubscriptionRepository.Get(email);
            if (existing != null && existing.EmailSubscriptionId != 0 && existing.IsSubscribed)
            {
                return;
            }

            // One confirmation email per address per window, so the form can't be used to flood
            // someone's inbox. Kept in memory only (keyed by a hash of the address, no IPs).
            var throttleKey = ThrottleKey(email);
            if (this.memoryCache.TryGetValue(throttleKey, out _))
            {
                return;
            }

            this.memoryCache.Set(throttleKey, true, ConfirmResendWindow);

            try
            {
                var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
                if (string.IsNullOrWhiteSpace(siteName))
                {
                    siteName = "our";
                }

                var link = $"{await this.GetAppBaseUrlAsync()}/newsletter/confirm?token={this.CreateConfirmToken(email)}";
                var hours = (int)ConfirmLinkLifetime.TotalHours;

                var subject = $"Confirm your {siteName} newsletter subscription";
                var text =
                    $"Please confirm that you want to get the {siteName} newsletter at {email}.\n\n" +
                    $"Confirm here: {link}\n\n" +
                    $"The link works for {hours} hours. If you did not ask for this, ignore this email and you will not be subscribed.\n";
                var html =
                    $"<p>Please confirm that you want to get the {WebUtility.HtmlEncode(siteName)} newsletter at {WebUtility.HtmlEncode(email)}.</p>" +
                    $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Confirm your subscription</a></p>" +
                    $"<p>The link works for {hours} hours. If you did not ask for this, ignore this email and you will not be subscribed.</p>";

                await this.emailService.SendEmailAsync(subject, text, html, new List<string> { email });
            }
            catch (Exception ex)
            {
                // Let the person try again right away; never log the address.
                this.memoryCache.Remove(throttleKey);
                this.logger.LogError(ex, "Newsletter confirmation email could not be sent");
            }
        }

        // Links in emails point at the dynamic app (the clearnet canonical site is a static mirror
        // that can't take a POST), from config like the unsubscribe links.
        private async Task<string> GetAppBaseUrlAsync()
        {
            var baseUrl = await this.cacheService.GetSnippetAsync(SiteConfigSetting.AppDomain);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{this.Request.Scheme}://{this.Request.Host}";
            }

            return baseUrl.Trim().TrimEnd('/');
        }

        private string? ReadConfirmToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            {
                return null;
            }

            try
            {
                var bytes = this.confirmProtector.Unprotect(Convert.FromHexString(token.Trim()), out _);
                var (ok, normalized, _) = EmailValidationHelper.Validate(Encoding.UTF8.GetString(bytes));
                return ok ? normalized : null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                // tampered, expired, or made with a different key
                return null;
            }
        }

        // Idempotent: a new address is subscribed (and gets the first campaign message), an
        // unsubscribed one is re-enabled, an active one is left alone.
        private async Task ActivateSubscriptionAsync(string email)
        {
            var emailDbModel = this.emailSubscriptionRepository.Get(email);

            if (emailDbModel != null && emailDbModel.EmailSubscriptionId != 0)
            {
                if (!emailDbModel.IsSubscribed)
                {
                    emailDbModel.IsSubscribed = true;
                    this.emailSubscriptionRepository.Update(emailDbModel);
                }

                return;
            }

            var emailSubscription = this.emailSubscriptionRepository.Create(new EmailSubscription()
            {
                Email = email,
                IsSubscribed = true
            });

            var campaigns = this.emailCampaignRepository.GetAll(0, int.MaxValue, out _);
            if (campaigns == null)
            {
                return;
            }

            foreach (var campaign in campaigns)
            {
                this.emailCampaignSubscriptionRepository.SubscribeToCampaign(
                    campaign.EmailCampaignId,
                    emailSubscription.EmailSubscriptionId);
            }

            var emailCampaign = this.emailCampaignRepository.GetDefault();
            if (emailCampaign == null)
            {
                return;
            }

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

            if (nextMessage == null)
            {
                return;
            }

            try
            {
                await this.emailService.SendEmailAsync(
                    nextMessage.EmailMessage.EmailSubject,
                    nextMessage.EmailMessage.EmailBodyText,
                    nextMessage.EmailMessage.EmailBodyHtml,
                    new List<string> { emailSubscription.Email });

                this.sentEmailRecordRepository.LogMessageDelivery(
                    emailSubscription.EmailSubscriptionId,
                    nextMessage.EmailMessageId);
            }
            catch (Exception ex)
            {
                // The subscription stands; the newsletter job sends this message on its next run.
                this.logger.LogError(ex, "First newsletter message could not be sent after confirmation");
            }
        }
    }
}
