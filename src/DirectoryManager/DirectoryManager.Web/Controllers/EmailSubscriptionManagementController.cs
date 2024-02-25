using System.Text;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class EmailSubscriptionManagementController : Controller
    {
        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;

        public EmailSubscriptionManagementController(
            IEmailSubscriptionRepository emailSubscriptionRepository)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
        }

        [Route("account/emailsubscriptionmanagement")]
        public IActionResult Index()
        {
            var allEmails = this.emailSubscriptionRepository.GetAll();
            var model = new EmailSubscribeEditListModel();
            model.TotalSubscribed = this.emailSubscriptionRepository.Total();

            foreach (var sub in allEmails)
            {
                model.Items.Add(new EmailSubscribeEditModel()
                {
                    Email = sub.Email,
                    IsSubscribed = sub.IsSubscribed,
                    EmailSubscriptionId = sub.EmailSubscriptionId
                });
            }

            if (allEmails != null && allEmails.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var sub in allEmails)
                {
                    if (!sub.IsSubscribed)
                    {
                        continue;
                    }

                    sb.AppendFormat("{0}, ", sub.Email);
                }

                model.Emails = sb.ToString();
                model.Emails = model.Emails.Trim().TrimEnd(',');
            }

            var link = string.Format(
                "{0}://{1}/emailsubscription/Unsubscribe",
                this.HttpContext.Request.Scheme,
                this.HttpContext.Request.Host);
            model.UnsubscribeLink = link;

            return this.View(model);
        }

        [Route("account/emailsubscriptionmanagement/edit")]
        [HttpPost]
        public IActionResult Edit(EmailSubscribeEditModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var dbModel = this.emailSubscriptionRepository.Get(model.EmailSubscriptionId);

            if (dbModel == null)
            {
                return this.NotFound();
            }

            dbModel.Email = model.Email;
            dbModel.IsSubscribed = model.IsSubscribed;

            this.emailSubscriptionRepository.Update(dbModel);

            return this.RedirectToAction("index");
        }

        [Route("account/emailsubscriptionmanagement/edit")]
        [HttpGet]
        public IActionResult Edit(int emailSubscriptionId)
        {
            var dbModel = this.emailSubscriptionRepository.Get(emailSubscriptionId);

            if (dbModel == null)
            {
                return this.NotFound();
            }

            var model = new EmailSubscribeEditModel()
            {
                Email = dbModel.Email,
                IsSubscribed = dbModel.IsSubscribed,
                EmailSubscriptionId = dbModel.EmailSubscriptionId
            };

            return this.View(model);
        }
    }
}