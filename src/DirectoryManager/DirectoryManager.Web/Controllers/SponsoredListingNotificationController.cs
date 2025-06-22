using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
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

        [HttpGet]
        [Route("sponsoredlistingnotification/subscribe")]
        public async Task<IActionResult> SubscribeAsync(
            SponsorshipType sponsorshipType,
            int? typeId)
        {
            var model = new SponsoredListingOpeningNotification
            {
                SponsorshipType = sponsorshipType,
                TypeId = typeId
            };

            if (typeId.HasValue)
            {
                if (sponsorshipType == SponsorshipType.SubcategorySponsor)
                {
                    var subcat = await this.subcategoryRepository
                                          .GetByIdAsync(typeId.Value)
                                          .ConfigureAwait(false);
                    var cat = await this.categoryRepository
                                        .GetByIdAsync(subcat.CategoryId)
                                        .ConfigureAwait(false);
                    this.TempData[StringConstants.SubcategoryName] =
                        FormattingHelper.SubcategoryFormatting(cat?.Name, subcat.Name);
                }
                else if (sponsorshipType == SponsorshipType.CategorySponsor)
                {
                    var cat = await this.categoryRepository
                                        .GetByIdAsync(typeId.Value)
                                        .ConfigureAwait(false);
                    this.TempData[StringConstants.CategoryName] = cat.Name;
                }
            }

            return this.View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/subscribe")]
        public async Task<IActionResult> Subscribe(SponsoredListingOpeningNotification model)
        {
            this.ModelState.Clear(); // ensure clean state
            model.Email = model.Email.Trim();
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                this.TempData[StringConstants.ErrorMessage] = "Valid email is required.";
                return this.RedirectToAction(
                    nameof(this.SubscribeAsync),
                    new { sponsorshipType = model.SponsorshipType, typeId = model.TypeId });
            }

            var exists = await this.notificationRepository
                                  .ExistsAsync(model.Email, model.SponsorshipType, model.TypeId)
                                  .ConfigureAwait(false);
            if (exists)
            {
                this.TempData[StringConstants.ErrorMessage] = "Already subscribed.";
                return this.View(model);
            }

            model.SubscribedDate = DateTime.UtcNow;
            model.IsReminderSent = false;
            await this.notificationRepository
                     .CreateAsync(model)
                     .ConfigureAwait(false);

            this.TempData[StringConstants.SuccessMessage] = "Subscription successful.";

            return this.View(model);
        }

        [Authorize, HttpGet, Route("sponsoredlistingnotification/list")]
        public async Task<IActionResult> List()
        {
            var list = await this.notificationRepository
                                 .GetAllAsync()
                                 .ConfigureAwait(false);
            return this.View(list);
        }

        [Authorize, HttpGet, Route("sponsoredlistingnotification/edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var notification = await this.notificationRepository
                                         .GetByIdAsync(id)
                                         .ConfigureAwait(false);
            if (notification == null)
            {
                this.TempData[StringConstants.ErrorMessage] = "Not found.";
                return this.RedirectToAction(nameof(this.List));
            }

            // Populate the friendly name for the view
            if (notification.SponsorshipType == SponsorshipType.SubcategorySponsor
                && notification.TypeId.HasValue)
            {
                var subcat = await this.subcategoryRepository
                                         .GetByIdAsync(notification.TypeId.Value)
                                         .ConfigureAwait(false);
                var cat = await this.categoryRepository
                                      .GetByIdAsync(subcat.CategoryId)
                                      .ConfigureAwait(false);
                this.TempData[StringConstants.SubcategoryName] =
                    FormattingHelper.SubcategoryFormatting(cat?.Name, subcat.Name);
            }
            else if (notification.SponsorshipType == SponsorshipType.CategorySponsor
                     && notification.TypeId.HasValue)
            {
                var cat = await this.categoryRepository
                                      .GetByIdAsync(notification.TypeId.Value)
                                      .ConfigureAwait(false);
                this.TempData[StringConstants.CategoryName] = cat.Name;
            }

            return this.View(notification);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken, Route("sponsoredlistingnotification/edit/{id}")]
        public async Task<IActionResult> Edit(SponsoredListingOpeningNotification model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var existing = await this.notificationRepository
                                     .GetByIdAsync(model.SponsoredListingOpeningNotificationId)
                                     .ConfigureAwait(false);
            if (existing == null)
            {
                this.TempData[StringConstants.ErrorMessage] = "Not found.";
                return this.RedirectToAction(nameof(this.List));
            }

            existing.Email = model.Email.Trim();
            existing.SponsorshipType = model.SponsorshipType;
            existing.TypeId = model.TypeId;
            existing.IsReminderSent = model.IsReminderSent;

            var ok = await this.notificationRepository
                               .UpdateAsync(existing)
                               .ConfigureAwait(false);
            this.TempData[ok ? StringConstants.SuccessMessage : StringConstants.ErrorMessage] =
                ok ? "Updated." : "Update failed.";

            return this.RedirectToAction(nameof(this.List));
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken, Route("sponsoredlistingnotification/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await this.notificationRepository
                               .DeleteAsync(id)
                               .ConfigureAwait(false);
            this.TempData[ok ? StringConstants.SuccessMessage : StringConstants.ErrorMessage] =
                ok ? "Deleted." : "Delete failed.";
            return this.RedirectToAction(nameof(this.List));
        }
    }
}
