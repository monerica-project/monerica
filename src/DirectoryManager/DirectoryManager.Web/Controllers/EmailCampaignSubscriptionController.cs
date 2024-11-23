using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("account/emailcampaignsubscription")]
    public class EmailCampaignSubscriptionController : Controller
    {
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;
        private readonly IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository;

        public EmailCampaignSubscriptionController(
            IEmailCampaignRepository emailCampaignRepository,
            IEmailSubscriptionRepository emailSubscriptionRepository,
            IEmailCampaignSubscriptionRepository emailCampaignSubscriptionRepository)
        {
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailSubscriptionRepository = emailSubscriptionRepository;
            this.emailCampaignSubscriptionRepository = emailCampaignSubscriptionRepository;
        }

        // GET: /account/emailcampaignsubscription/{campaignId}
        [HttpGet("{campaignId}")]
        public IActionResult Index(int campaignId)
        {
            var subscriptions = this.emailCampaignSubscriptionRepository.GetByCampaign(campaignId);
            this.ViewBag.CampaignName = this.emailCampaignRepository.Get(campaignId)?.Name;
            this.ViewBag.CampaignId = campaignId;
            return this.View(subscriptions);
        }

        [HttpGet("Subscribe/{campaignId}")]
        public IActionResult Subscribe(int campaignId)
        {
            var campaign = this.emailCampaignRepository.Get(campaignId);
            if (campaign == null)
            {
                return this.NotFound();
            }

            this.ViewBag.CampaignId = campaignId;
            this.ViewBag.CampaignName = campaign.Name;
            this.ViewBag.CampaignIntervalDays = campaign.IntervalDays;
            this.ViewBag.CampaignStartDate = campaign.StartDate?.ToString(Common.Constants.StringConstants.DateFormat);
            this.ViewBag.EmailSubscriptions = this.emailSubscriptionRepository.GetAll();
            return this.View();
        }

        // POST: /account/emailcampaignsubscription/Subscribe/{campaignId}
        [HttpPost("Subscribe/{campaignId}")]
        [ValidateAntiForgeryToken]
        public IActionResult Subscribe(int campaignId, int emailSubscriptionId)
        {
            var subscription = this.emailCampaignSubscriptionRepository.SubscribeToCampaign(campaignId, emailSubscriptionId);
            return this.RedirectToAction("Index", new { campaignId = subscription.EmailCampaignId });
        }

        // POST: /account/emailcampaignsubscription/unsubscribe
        [HttpPost("Unsubscribe")]
        [ValidateAntiForgeryToken]
        public IActionResult Unsubscribe(int campaignId, int emailSubscriptionId)
        {
            this.emailCampaignSubscriptionRepository.UnsubscribeFromCampaign(campaignId, emailSubscriptionId);
            return this.RedirectToAction("Index", new { campaignId });
        }
    }
}