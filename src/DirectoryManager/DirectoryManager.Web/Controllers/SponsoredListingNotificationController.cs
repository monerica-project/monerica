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
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        public SponsoredListingNotificationController(
            ISponsoredListingOpeningNotificationRepository notificationRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepository,
            ISponsoredListingRepository sponsoredListingRepository)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.additionalSubcategoryRepository = additionalSubcategoryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.notificationRepository = notificationRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
        }

        [Authorize]
        [HttpGet]
        [Route("sponsoredlistingnotification/list")]
        public async Task<IActionResult> List(int page = 1, int pageSize = 50, SponsorshipType? type = null, int? typeId = null)
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

            List<SponsoredListingOpeningNotification> ordered;

            if (type is SponsorshipType filterType && filterType != SponsorshipType.Unknown)
            {
                // One placement's waitlist: only people still waiting, first in line at the top.
                var filterId = filterType == SponsorshipType.MainSponsor || typeId is null or <= 0 ? null : typeId;
                ordered = all
                    .Where(x => x.IsActive && !x.IsReminderSent && x.SponsorshipType == filterType)
                    .Where(x => filterId == null ? (x.TypeId == null || x.TypeId == 0) : x.TypeId == filterId)
                    .OrderBy(x => x.SubscribedDate)
                    .ToList();

                this.ViewBag.FilterType = filterType;
                this.ViewBag.FilterTypeId = filterId;
                this.ViewBag.FilterLabel = filterType switch
                {
                    SponsorshipType.MainSponsor => "Main sponsor",
                    SponsorshipType.CategorySponsor when filterId is int c =>
                        "Category: " + ((await this.categoryRepository.GetByIdAsync(c))?.Name ?? $"#{c}"),
                    SponsorshipType.SubcategorySponsor when filterId is int sc =>
                        "Subcategory: " + ((await this.subcategoryRepository.GetByIdAsync(sc))?.Name ?? $"#{sc}"),
                    _ => filterType.ToString(),
                };
            }
            else
            {
                ordered = all
                    .OrderByDescending(x => x.CreateDate)
                    .ToList();
            }

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

            // Listing names + whether each row's listing could buy that placement right now
            // (same rules as checkout: SponsorshipEligibilityHelper.ForPlacement).
            var listingNames = new Dictionary<int, string>();
            var entries = new Dictionary<int, DirectoryManager.Data.Models.DirectoryEntry>();
            foreach (var entryId in items.Where(x => x.DirectoryEntryId is > 0).Select(x => x.DirectoryEntryId!.Value).Distinct())
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(entryId);
                if (entry != null)
                {
                    listingNames[entryId] = entry.Name;
                    entries[entryId] = entry;
                }
            }

            var subcategories = (await this.subcategoryRepository.GetAllAsync()).ToDictionary(s => s.SubCategoryId);
            var activeSponsors = await this.sponsoredListingRepository.GetActivelySponsoredDirectoryEntryIdsAsync();
            var eligibility = new Dictionary<int, (bool Eligible, string? Reason)>();
            var now = DateTime.UtcNow;

            foreach (var n in items)
            {
                if (n.DirectoryEntryId is not int eid || !entries.TryGetValue(eid, out var e))
                {
                    eligibility[n.SponsoredListingOpeningNotificationId] = (false, "No listing linked");
                    continue;
                }

                subcategories.TryGetValue(e.SubCategoryId, out var mainSub);
                var alts = (await this.additionalSubcategoryRepository.GetByDirectoryEntryIdAsync(eid))
                    .Select(a => (a.SubCategoryId, subcategories.TryGetValue(a.SubCategoryId, out var s) && s.SponsorshipDisabled))
                    .ToList();

                eligibility[n.SponsoredListingOpeningNotificationId] = SponsorshipEligibilityHelper.ForPlacement(
                    n.SponsorshipType,
                    n.TypeId,
                    e.DirectoryStatus,
                    e.CreateDate,
                    e.SubCategoryId,
                    mainSub?.CategoryId,
                    mainSub?.SponsorshipDisabled == true,
                    alts,
                    activeSponsors.Contains(eid),
                    DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring,
                    now);
            }

            this.ViewBag.ListingNames = listingNames;
            this.ViewBag.Eligibility = eligibility;

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

            // Keep IsActive in sync with the "reminder sent" flag so the edit page
            // actually re-queues someone. The opening job only emails rows where
            // IsActive == true AND IsReminderSent == false; when a reminder is sent,
            // MarkReminderAsSentAsync sets IsReminderSent = true AND IsActive = false.
            // Previously this POST changed IsReminderSent but left IsActive = false,
            // so un-checking "reminder sent" produced IsActive=false/IsReminderSent=false
            // — a state the job silently skips, and the email never went out.
            if (model.IsReminderSent)
            {
                // Marked as already sent: take them out of the active queue.
                existing.IsActive = false;
            }
            else
            {
                // Marked as not-yet-sent: put them back in the queue and clear the
                // sent audit so the next 30-minute run will email them (if a slot is open).
                existing.IsActive = true;
                existing.ReminderSentDateUtc = null;
                existing.ReminderSentLink = null;
            }

            existing.UpdateDate = DateTime.UtcNow;

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
        public IActionResult SubscribeAsync(SponsorshipType sponsorshipType, int? typeId)
        {
            return this.RedirectPermanent("/sponsorship");
        }
    }
}