using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("account/emailcampaignmanagement")]
    public class EmailCampaignManagementController : Controller
    {
        private readonly IEmailCampaignRepository emailCampaignRepository;
        private readonly IEmailMessageRepository emailMessageRepository;
        private readonly IEmailCampaignMessageRepository emailCampaignMessageRepository;

        public EmailCampaignManagementController(
            IEmailCampaignRepository emailCampaignRepository,
            IEmailMessageRepository emailMessageRepository,
            IEmailCampaignMessageRepository emailCampaignMessageRepository)
        {
            this.emailCampaignRepository = emailCampaignRepository;
            this.emailMessageRepository = emailMessageRepository;
            this.emailCampaignMessageRepository = emailCampaignMessageRepository;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index(int page = 1)
        {
            const int pageSize = IntegerConstants.MaxPageSize;
            this.emailCampaignRepository.GetAll(page - 1, pageSize, out int totalItems);

            var model = new PagedEmailCampaignModel
            {
                Campaigns = this.emailCampaignRepository.GetAll(page - 1, pageSize, out totalItems),
                PageIndex = page,
                TotalItems = totalItems,
                PageSize = pageSize
            };

            return this.View("Index", model);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            this.ViewBag.AvailableMessages = this.emailMessageRepository.GetAll(0, this.emailMessageRepository.TotalCount());
            return this.View(new EmailCampaignModel());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(EmailCampaignModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.ViewBag.AvailableMessages = this.emailMessageRepository.GetAll(0, this.emailMessageRepository.TotalCount());
                return this.View(model);
            }

            var newCampaign = new EmailCampaign
            {
                Name = model.Name,
                IntervalDays = model.IntervalDays,
                StartDate = model.StartDate
            };

            this.emailCampaignRepository.Create(newCampaign);

            foreach (var messageId in model.CampaignMessages.Select(m => m.EmailMessageId))
            {
                var campaignMessage = new EmailCampaignMessage
                {
                    EmailCampaignId = newCampaign.EmailCampaignId,
                    EmailMessageId = messageId,
                    SequenceOrder = model.CampaignMessages.FindIndex(m => m.EmailMessageId == messageId) + 1
                };
                this.emailCampaignMessageRepository.Create(campaignMessage);
            }

            return this.RedirectToAction("Index");
        }

        [HttpGet("Edit/{id}")]
        public IActionResult Edit(int id)
        {
            var campaign = this.emailCampaignRepository.Get(id);
            if (campaign == null)
            {
                return this.NotFound();
            }

            var model = new EmailCampaignModel
            {
                SendMessagesPriorToSubscription = campaign.SendMessagesPriorToSubscription,
                IsEnabled = campaign.IsEnabled,
                IsDefault = campaign.IsDefault,
                EmailCampaignId = campaign.EmailCampaignId,
                Name = campaign.Name,
                IntervalDays = campaign.IntervalDays,
                StartDate = campaign.StartDate,
                CampaignMessages = campaign.CampaignMessages
                    .OrderBy(m => m.SequenceOrder)
                    .Select(m => new EmailCampaignMessageModel
                    {
                        EmailMessageId = m.EmailMessageId,
                        SequenceOrder = m.SequenceOrder,
                        EmailCampaignMessageId = m.EmailCampaignMessageId
                    }).ToList()
            };

            this.ViewBag.AvailableMessages = this.emailMessageRepository.GetAll(0, this.emailMessageRepository.TotalCount());
            return this.View(model);
        }

        [HttpPost("Edit/{id}/AddMessage")]
        [ValidateAntiForgeryToken]
        public IActionResult AddMessage(int id, int selectedMessageId)
        {
            var campaign = this.emailCampaignRepository.Get(id);
            if (campaign == null || selectedMessageId == 0)
            {
                return this.NotFound();
            }

            var maxSequenceOrder = campaign.CampaignMessages.Any()
                ? campaign.CampaignMessages.Max(m => m.SequenceOrder)
                : 0;

            var newMessage = new EmailCampaignMessage
            {
                EmailCampaignId = id,
                EmailMessageId = selectedMessageId,
                SequenceOrder = maxSequenceOrder + 1
            };

            this.emailCampaignMessageRepository.Create(newMessage);

            return this.RedirectToAction("Edit", new { id });
        }

        [HttpPost("Edit/{id}/DeleteMessage")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteMessage(int id, int emailCampaignMessageId)
        {
            var result = this.emailCampaignMessageRepository.Delete(emailCampaignMessageId);
            if (!result)
            {
                return this.NotFound();
            }

            return this.RedirectToAction("Edit", new { id });
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EmailCampaignModel model)
        {
            if (!this.ModelState.IsValid)
            {
                this.ViewBag.AvailableMessages = this.emailMessageRepository.GetAll(0, this.emailMessageRepository.TotalCount());
                return this.View(model);
            }

            var campaign = this.emailCampaignRepository.Get(model.EmailCampaignId);
            if (campaign == null)
            {
                return this.NotFound();
            }

            campaign.Name = model.Name;
            campaign.IntervalDays = model.IntervalDays;
            campaign.StartDate = model.StartDate;
            campaign.IsDefault = model.IsDefault;
            campaign.SendMessagesPriorToSubscription = model.SendMessagesPriorToSubscription;
            campaign.IsEnabled = model.IsEnabled;
            this.emailCampaignRepository.Update(campaign);

            this.emailCampaignMessageRepository.Delete(campaign.EmailCampaignId);

            foreach (var messageId in model.CampaignMessages.Select(m => m.EmailMessageId))
            {
                var campaignMessage = new EmailCampaignMessage
                {
                    EmailCampaignId = campaign.EmailCampaignId,
                    EmailMessageId = messageId,
                    SequenceOrder = model.CampaignMessages.FindIndex(m => m.EmailMessageId == messageId) + 1
                };
                this.emailCampaignMessageRepository.Create(campaignMessage);
            }

            return this.RedirectToAction("Index");
        }
    }
}