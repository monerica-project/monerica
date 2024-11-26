using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingNotificationController : Controller
    {
        private readonly ISponsoredListingOpeningNotificationRepository notificationRepository;

        public SponsoredListingNotificationController(ISponsoredListingOpeningNotificationRepository notificationRepository)
        {
            this.notificationRepository = notificationRepository;
        }

        [Route("sponsoredlistingnotification/subscribe")]
        [HttpGet]
        public IActionResult Subscribe(SponsorshipType sponsorshipType, int? subCategoryId)
        {
            var model = new SponsoredListingOpeningNotification
            {
                SponsorshipType = sponsorshipType,
                SubCategoryId = subCategoryId
            };

            return this.View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/subscribe")]
        public async Task<IActionResult> Subscribe(SponsoredListingOpeningNotification model)
        {
            model.Email = InputHelper.SetEmail(model.Email);

            if (string.IsNullOrEmpty(model.Email))
            {
                if (string.IsNullOrEmpty(model.Email))
                {
                    this.TempData[Constants.StringConstants.ErrorMessage] = "Valid email address is required.";
                    return this.RedirectToAction(nameof(this.Subscribe), new { sponsorshipType = model.SponsorshipType, subCategoryId = model.SubCategoryId });
                }
            }

            // Check if the user is already subscribed
            var alreadySubscribed = await this.notificationRepository.ExistsAsync(
                model.Email,
                model.SponsorshipType,
                model.SubCategoryId);

            if (alreadySubscribed)
            {
                this.TempData[Constants.StringConstants.ErrorMessage] = "You have already subscribed for this notification.";
                return this.View(model);
            }

            // Add the subscription
            model.SubscribedDate = DateTime.UtcNow;
            model.IsReminderSent = false;

            await this.notificationRepository.CreateAsync(model);

            this.TempData[Constants.StringConstants.SuccessMessage] = "You have successfully subscribed to the notification.";

            // Redirect back to the same subscription page with the parameters
            return this.RedirectToAction(nameof(this.Subscribe), new { sponsorshipType = model.SponsorshipType, subCategoryId = model.SubCategoryId });
        }
    }
}
