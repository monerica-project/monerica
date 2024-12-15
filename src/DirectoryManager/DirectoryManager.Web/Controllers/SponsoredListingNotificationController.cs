using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingNotificationController : Controller
    {
        private readonly ISponsoredListingOpeningNotificationRepository notificationRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;

        public SponsoredListingNotificationController(
            ISponsoredListingOpeningNotificationRepository notificationRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository)
        {
            this.notificationRepository = notificationRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
        }

        [Route("sponsoredlistingnotification/subscribe")]
        [HttpGet]
        public async Task<IActionResult> SubscribeAsync(SponsorshipType sponsorshipType, int? subcategoryId)
        {
            var model = new SponsoredListingOpeningNotification
            {
                SponsorshipType = sponsorshipType,
                SubCategoryId = subcategoryId
            };

            if (subcategoryId != null)
            {
                var subcategory = await this.subcategoryRepository.GetByIdAsync(subcategoryId.Value);

                if (subcategory != null)
                {
                    var category = await this.categoryRepository.GetByIdAsync(subcategory.CategoryId);
                    this.TempData[Constants.StringConstants.SubcategoryName] = FormattingHelper.SubcategoryFormatting(category?.Name, subcategory.Name);
                }
            }

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

        // Existing constructor and other methods...
        [Authorize]
        [HttpGet]
        [Route("sponsoredlistingnotification/list")]
        public async Task<IActionResult> List()
        {
            var notifications = await this.notificationRepository.GetAllAsync();
            return this.View(notifications);
        }

        [Authorize]
        [HttpGet]
        [Route("sponsoredlistingnotification/edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var notification = await this.notificationRepository.GetByIdAsync(id);

            if (notification == null)
            {
                this.TempData[Constants.StringConstants.ErrorMessage] = "Notification not found.";
                return this.RedirectToAction(nameof(this.List));
            }

            return this.View(notification);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/edit/{id}")]
        public async Task<IActionResult> Edit(SponsoredListingOpeningNotification model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var existingNotification = await this.notificationRepository.GetByIdAsync(model.SponsoredListingOpeningNotificationId);
            if (existingNotification == null)
            {
                this.TempData[Constants.StringConstants.ErrorMessage] = "Notification not found.";
                return this.RedirectToAction(nameof(this.List));
            }

            // Update notification
            existingNotification.Email = model.Email;
            existingNotification.SponsorshipType = model.SponsorshipType;
            existingNotification.SubCategoryId = model.SubCategoryId;
            existingNotification.IsReminderSent = model.IsReminderSent;

            var updated = await this.notificationRepository.UpdateAsync(existingNotification);
            if (updated)
            {
                this.TempData[Constants.StringConstants.SuccessMessage] = "Notification updated successfully.";
            }
            else
            {
                this.TempData[Constants.StringConstants.ErrorMessage] = "Failed to update notification.";
            }

            return this.RedirectToAction(nameof(this.List));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await this.notificationRepository.DeleteAsync(id);

            if (deleted)
            {
                this.TempData[Constants.StringConstants.SuccessMessage] = "Notification deleted successfully.";
            }
            else
            {
                this.TempData[Constants.StringConstants.ErrorMessage] = "Failed to delete notification. It may not exist.";
            }

            return this.RedirectToAction(nameof(this.List));
        }
    }
}
