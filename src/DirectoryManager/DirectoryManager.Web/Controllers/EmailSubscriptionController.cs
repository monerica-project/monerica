using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
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
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;
        private readonly ICacheService cacheService;

        public EmailSubscriptionController(
            IEmailSubscriptionRepository emailSubscriptionRepository,
            IBlockedIPRepository blockedIPRepository,
            IEmailCampaignRepository emailCampaignRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository,
            ICacheService cacheService)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
            this.blockedIPRepository = blockedIPRepository;
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
            this.cacheService = cacheService;
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
        public IActionResult Subscribe(EmailSubscribeModel model)
        {
            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            if (!this.ModelState.IsValid ||
                !ValidationHelper.IsValidEmail(model.Email) ||
                this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.BadRequest("Invalid email");
            }

            var emailDbModel = this.emailSubscriptionRepository.Get(model.Email);

            if (emailDbModel == null || emailDbModel.EmailSubscriptionId == 0)
            {
                var emailSubscription = this.emailSubscriptionRepository.Create(new EmailSubscription()
                {
                    Email = model.Email,
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
                }
            }

            return this.View("ConfirmSubscribed");
        }
    }
}