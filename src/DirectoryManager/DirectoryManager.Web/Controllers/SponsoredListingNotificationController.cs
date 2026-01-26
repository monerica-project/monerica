using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
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
        public async Task<IActionResult> List(int page = 1, int pageSize = 50)
        {
            // hard guards
            if (page < 1)
            {
                page = 1;
            }

            // keep pageSize sane (adjust max as you like)
            if (pageSize < 5)
            {
                pageSize = 5;
            }

            if (pageSize > 200)
            {
                pageSize = 200;
            }

            var all = await this.notificationRepository
                                 .GetAllAsync()
                                 .ConfigureAwait(false);

            var ordered = all
                .OrderByDescending(x => x.CreateDate)
                .ToList();

            var totalCount = ordered.Count;

            // if page is out of range, snap to last page (unless empty)
            var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new PagedListViewModel<SponsoredListingOpeningNotification>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return this.View(vm);
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

                if (subcat == null)
                {
                    return this.View(notification);
                }

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

                if (cat == null)
                {
                    return this.View(notification);
                }

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
            // Validate email early
            var (okEmail, normalizedEmail, emailError) = EmailValidationHelper.Validate(model.Email);
            if (!okEmail)
            {
                this.ModelState.AddModelError("Email", emailError!);
            }

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

            existing.Email = normalizedEmail!;
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
            // Captcha context
            var ctx = (this.Request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ctx))
            {
                ctx = "sponsoredlistingnotification";
            }

            // Validate CAPTCHA (consume so it can't be replayed)
            var captchaOk = CaptchaTools.Validate(this.HttpContext, ctx, vm.Captcha, consume: true);
            if (!captchaOk)
            {
                // Keep your existing view-level messaging pattern
                vm.ErrorMessage = "Incorrect CAPTCHA. Please try again.";
                return this.View(vm);
            }

            vm.Email = (vm.Email ?? string.Empty).Trim();

            // Validate email with the shared helper
            var (okEmail, normalizedEmail, emailError) = EmailValidationHelper.Validate(vm.Email);
            if (!okEmail)
            {
                vm.ErrorMessage = emailError!;
                return this.View(vm);
            }

            vm.Email = normalizedEmail!;

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