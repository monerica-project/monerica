using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
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

        [Authorize]
        [HttpGet]
        [Route("sponsoredlistingnotification/list")]
        public async Task<IActionResult> List()
        {
            var list = await this.notificationRepository
                                 .GetAllAsync()
                                 .ConfigureAwait(false);
            return this.View(list);
        }

        [Authorize]
        [HttpGet]
        [Route("sponsoredlistingnotification/edit/{id}")]
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

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await this.notificationRepository
                               .DeleteAsync(id)
                               .ConfigureAwait(false);
            this.TempData[ok ? StringConstants.SuccessMessage : StringConstants.ErrorMessage] =
                ok ? "Deleted." : "Delete failed.";
            return this.RedirectToAction(nameof(this.List));
        }

        [HttpGet]
        [Route("sponsoredlistingnotification/subscribe")]
        public async Task<IActionResult> SubscribeAsync(
            SponsorshipType sponsorshipType,
            int? typeId)
        {
            var vm = new SubscribeViewModel
            {
                SponsorshipType = sponsorshipType,
                TypeId = typeId
            };

            // populate friendly label
            if (typeId.HasValue)
            {
                if (sponsorshipType == SponsorshipType.SubcategorySponsor)
                {
                    var subcat = await this.subcategoryRepository.GetByIdAsync(typeId.Value);
                    var cat = await this.categoryRepository.GetByIdAsync(subcat.CategoryId);
                    vm.CategoryOrSubcategoryName =
                        FormattingHelper.SubcategoryFormatting(cat?.Name, subcat.Name);
                }
                else if (sponsorshipType == SponsorshipType.CategorySponsor)
                {
                    var cat = await this.categoryRepository.GetByIdAsync(typeId.Value);
                    vm.CategoryOrSubcategoryName = cat.Name;
                }
            }

            return this.View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingnotification/subscribe")]
        public async Task<IActionResult> Subscribe(SubscribeViewModel vm)
        {
            vm.Email = (vm.Email ?? "").Trim();

            if (string.IsNullOrWhiteSpace(vm.Email))
            {
                vm.ErrorMessage = "A valid email is required.";
                return this.View(vm);
            }

            if (await this.notificationRepository
                      .ExistsAsync(vm.Email, vm.SponsorshipType, vm.TypeId))
            {
                vm.ErrorMessage = "You’re already subscribed.";
                return this.View(vm);
            }

            var notif = new SponsoredListingOpeningNotification
            {
                Email = vm.Email,
                SponsorshipType = vm.SponsorshipType,
                TypeId = vm.TypeId,
                SubscribedDate = DateTime.UtcNow,
                IsReminderSent = false
            };
            await this.notificationRepository.CreateAsync(notif);

            vm.SuccessMessage = "Subscription successful!";

            return this.View(vm);
        }
    }
}
