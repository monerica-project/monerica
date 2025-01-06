using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;
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

        [Route("unsubscribe")]
        [AcceptVerbs("GET", "POST")]
        public IActionResult Unsubscribe([FromQuery] string? email, [FromForm] string? formEmail)
        {
            var finalEmail = !string.IsNullOrWhiteSpace(email) ? email : formEmail;

            if (string.IsNullOrWhiteSpace(finalEmail))
            {
                return this.View(nameof(this.Unsubscribe), null);
            }

            formEmail = formEmail?.Trim();

            var subscription = this.emailSubscriptionRepository.Get(finalEmail);
            if (subscription == null)
            {
                this.ModelState.AddModelError(string.Empty, "Email not found in our subscription list.");
                return this.View(nameof(this.Unsubscribe), null);
            }

            subscription.IsSubscribed = false;
            this.emailSubscriptionRepository.Update(subscription);

            return this.View(nameof(this.Unsubscribe), finalEmail);
        }

        [Route("newsletter")]
        [Route("subscribe")]
        [HttpGet]
        public IActionResult Subscribe()
        {
            this.ViewBag.NewsletterSummaryHtml = this.cacheService.GetSnippet(Data.Enums.SiteConfigSetting.NewsletterSummaryHtml);
            return this.View();
        }

        [Route("newsletter")]
        [Route("subscribe")]
        [HttpPost]
        public async Task<IActionResult> Subscribe(EmailSubscribeModel model)
        {
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
                    IsSubscribed = true
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
    }
}