using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class EmailMessageManagementController : Controller
    {
        private readonly IEmailMessageRepository emailMessageRepository;

        public EmailMessageManagementController(IEmailMessageRepository emailMessageRepository)
        {
            this.emailMessageRepository = emailMessageRepository;
        }

        [Route("account/emailmessagemanagement")]
        public IActionResult Index(int pageIndex = 0, int pageSize = IntegerConstants.DefaultPageSize)
        {
            var totalMessages = this.emailMessageRepository.TotalCount();
            var messages = this.emailMessageRepository.GetAll(pageIndex, pageSize);
            var model = new EmailMessageListModel
            {
                Messages = messages.Select(e => new EmailMessageModel
                {
                    EmailMessageId = e.EmailMessageId,
                    EmailKey = e.EmailKey,
                    EmailSubject = e.EmailSubject
                }).ToList(),
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalMessages
            };
            return this.View("Index", model);
        }

        [HttpGet("account/emailmessagemanagement/create")]
        public IActionResult Create() => this.View(new EmailMessageModel());

        [HttpPost("account/emailmessagemanagement/create")]
        public IActionResult Create(EmailMessageModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var newEmailMessage = new EmailMessage
            {
                EmailKey = StringHelpers.UrlKey(model.EmailKey),
                EmailSubject = model.EmailSubject,
                EmailBodyText = model.EmailBodyText,
                EmailBodyHtml = model.EmailBodyHtml
            };
            this.emailMessageRepository.Create(newEmailMessage);

            return this.RedirectToAction("Index");
        }

        [HttpGet("account/emailmessagemanagement/edit/{id}")]
        public IActionResult Edit(int id)
        {
            var emailMessage = this.emailMessageRepository.Get(id);
            if (emailMessage == null)
            {
                return this.NotFound();
            }

            var model = new EmailMessageModel
            {
                EmailMessageId = emailMessage.EmailMessageId,
                EmailKey = emailMessage.EmailKey,
                EmailSubject = emailMessage.EmailSubject,
                EmailBodyText = emailMessage.EmailBodyText,
                EmailBodyHtml = emailMessage.EmailBodyHtml
            };
            return this.View(model);
        }

        [HttpPost("account/emailmessagemanagement/edit/{id}")]
        public IActionResult Edit(EmailMessageModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var emailMessage = this.emailMessageRepository.Get(model.EmailMessageId);
            if (emailMessage == null)
            {
                return this.NotFound();
            }

            emailMessage.EmailKey = StringHelpers.UrlKey(model.EmailKey);
            emailMessage.EmailSubject = model.EmailSubject;
            emailMessage.EmailBodyText = model.EmailBodyText;
            emailMessage.EmailBodyHtml = model.EmailBodyHtml;
            this.emailMessageRepository.Update(emailMessage);

            return this.RedirectToAction("Index");
        }

        [HttpPost("account/emailmessagemanagement/delete/{id}")]
        public IActionResult Delete(int id)
        {
            this.emailMessageRepository.Delete(id);
            return this.RedirectToAction("Index");
        }
    }
}