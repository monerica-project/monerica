using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class EmailSubscriptionController : Controller
    {
        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;

        public EmailSubscriptionController(
            IEmailSubscriptionRepository emailSubscriptionRepository)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
        }

        [Route("newsletter")]
        [Route("subscribe")]
        [HttpGet]
        public IActionResult Subscribe()
        {
            return this.View();
        }

        [Route("subscribe")]
        [HttpPost]
        public IActionResult Subscribe(EmailSubscribeModel model)
        {
            if (!this.ModelState.IsValid || !ValidationHelpers.IsValidEmail(model.Email))
            {
                return this.BadRequest("Invalid email");
            }

            var emailDbModel = this.emailSubscriptionRepository.Get(model.Email);

            if (emailDbModel == null || emailDbModel.EmailSubscriptionId == 0)
            {
                this.emailSubscriptionRepository.Create(new Data.Models.EmailSubscription()
                {
                    Email = model.Email,
                    IsSubscribed = true
                });
            }

            return this.View("ConfirmSubscribed");
        }
    }
}