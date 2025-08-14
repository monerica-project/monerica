using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingController : BaseController
    {
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly INowPaymentsService paymentService;
        private readonly IMemoryCache cache;
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISponsoredListingReservationRepository sponsoredListingReservationRepository;
        private readonly IBlockedIPRepository blockedIPRepository;
        private readonly ICacheService cacheService;
        private readonly ILogger<SponsoredListingController> logger;

        public SponsoredListingController(
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ITrafficLogRepository trafficLogRepository,
            INowPaymentsService paymentService,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            ISponsoredListingOfferRepository sponsoredListings,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            IBlockedIPRepository blockedIPRepository,
            ICacheService cacheService,
            ILogger<SponsoredListingController> logger)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.paymentService = paymentService;
            this.cache = cache;
            this.sponsoredListingOfferRepository = sponsoredListings;
            this.sponsoredListingReservationRepository = sponsoredListingReservationRepository;
            this.cacheService = cacheService;
            this.blockedIPRepository = blockedIPRepository;
            this.logger = logger;
        }

        [Route("advertising")]
        [Route("sponsoredlisting")]
        public async Task<IActionResult> IndexAsync()
        {
            var mainSponsorType = SponsorshipType.MainSponsor;
            var mainSponsorReservationGroup = ReservationGroupHelper.BuildReservationGroupName(mainSponsorType, 0);
            var currentMainSponsorListings = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(mainSponsorType);
            var model = new SponsoredListingHomeModel();

            if (currentMainSponsorListings != null && currentMainSponsorListings.Any())
            {
                var count = currentMainSponsorListings.Count();
                model.CurrentListingCount = count;

                if (count >= Common.Constants.IntegerConstants.MaxMainSponsoredListings)
                {
                    model.CanCreateMainListing = false;
                }
                else
                {
                    var totalActiveListings = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(mainSponsorType, null);
                    var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(mainSponsorReservationGroup);

                    if (CanPurchaseListing(totalActiveListings, totalActiveReservations, mainSponsorType))
                    {
                        model.CanCreateMainListing = true;
                    }
                    else
                    {
                        // UPDATED: pass type + scoped typeId (null for MainSponsor) + group
                        model.Message = await this.BuildCheckoutInProcessMessageAsync(SponsorshipType.MainSponsor, null, mainSponsorReservationGroup);
                        model.CanCreateMainListing = false;
                    }
                }

                model.NextListingExpiration = currentMainSponsorListings.Min(x => x.CampaignEndDate);
            }
            else
            {
                model.CanCreateMainListing = true;
            }

            var allActiveSubcategories = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync(Common.Constants.IntegerConstants.MinRequiredSubcategories);
            var currentSubSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);

            if (currentSubSponsors != null)
            {
                foreach (var sc in allActiveSubcategories)
                {
                    var label = FormattingHelper.SubcategoryFormatting(sc.Category.Name, sc.Name);
                    var sponsor = currentSubSponsors.FirstOrDefault(x => x.SubCategoryId == sc.SubCategoryId);

                    if (sponsor != null)
                    {
                        model.UnavailableSubCatetgories.Add(sc.SubCategoryId, label);
                        model.UnavailableSubcategoryExpirations[sc.SubCategoryId] = sponsor.CampaignEndDate;
                    }
                    else
                    {
                        model.AvailableSubCatetgories.Add(sc.SubCategoryId, label);
                    }
                }

                model.AvailableSubCatetgories = model.AvailableSubCatetgories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                model.UnavailableSubCatetgories = model.UnavailableSubCatetgories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            var allCategories = await this.categoryRepository.GetAllAsync();
            var currentCatSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);

            if (currentCatSponsors != null)
            {
                foreach (var cat in allCategories)
                {
                    var label = cat.Name;

                    var sponsor = currentCatSponsors.FirstOrDefault(x =>
                        x.DirectoryEntry?.SubCategory != null &&
                        x.DirectoryEntry.SubCategory.CategoryId == cat.CategoryId);

                    if (sponsor != null)
                    {
                        model.UnavailableCategories.Add(cat.CategoryId, label);
                        model.UnavailableCategoryExpirations[cat.CategoryId] = sponsor.CampaignEndDate;
                    }
                    else
                    {
                        model.AvailableCategories.Add(cat.CategoryId, label);
                    }
                }

                model.AvailableCategories = model.AvailableCategories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                model.UnavailableCategories = model.UnavailableCategories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            return this.View(model);
        }


        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectlisting")]
        public async Task<IActionResult> SelectListing(
            SponsorshipType sponsorshipType = SponsorshipType.MainSponsor,
            int? subCategoryId = null,
            int? categoryId = null)
        {
            if (!subCategoryId.HasValue && !categoryId.HasValue)
            {
                if (sponsorshipType == SponsorshipType.CategorySponsor)
                {
                    return this.Redirect(this.Url.Content("~/sponsoredlisting#category-sponsor-section"));
                }

                if (sponsorshipType == SponsorshipType.SubcategorySponsor)
                {
                    return this.Redirect(this.Url.Content("~/sponsoredlisting#subcategory-sponsor-section"));
                }
            }

            var typeValue = subCategoryId ?? categoryId;
            var totalActiveListings = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(sponsorshipType, typeValue);

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                if (subCategoryId != null)
                {
                    var totalActiveEntriesInCategory = await this.directoryEntryRepository.GetActiveEntriesBySubcategoryAsync(subCategoryId.Value);

                    this.ViewBag.CanAdvertise =
                        totalActiveListings < Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings &&
                        totalActiveEntriesInCategory.Count() >= Common.Constants.IntegerConstants.MinRequiredSubcategories;
                }
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor && categoryId.HasValue)
            {
                var entriesInCat = await this.directoryEntryRepository.GetActiveEntriesByCategoryAsync(categoryId.Value).ConfigureAwait(false);
                this.ViewBag.CanAdvertise =
                    totalActiveListings < Common.Constants.IntegerConstants.MaxCategorySponsoredListings &&
                    entriesInCat.Count() >= Common.Constants.IntegerConstants.MinRequiredCategories;
            }

            this.ViewBag.SponsorshipType = sponsorshipType;

            var entries = await this.FilterEntries(subCategoryId, categoryId);
            return this.View("SelectListing", entries);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
            int directoryEntryId,
            SponsorshipType sponsorshipType,
            Guid? rsvId = null)
        {
            if (sponsorshipType == SponsorshipType.Unknown)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSponsorshipType });
            }

            var entry = await this.directoryEntryRepository
                .GetByIdAsync(directoryEntryId)
                .ConfigureAwait(false);
            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            if (!this.IsOldEnough(entry))
            {
                return this.BadRequest(new
                {
                    Error = $"Unverified listing must be listed for at least {IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising} days before advertising."
                });
            }

            var typeIdForGroup = sponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                _ => 0,
            };
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);

            // capacity queries should use null for MainSponsor (repo expects null there)
            int? typeIdForCapacity = sponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            // Is this an extension? (holder already owns the slot)
            var isExtension = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, sponsorshipType)
                .ConfigureAwait(false);

            // Validate existing reservation (if any)
            if (rsvId.HasValue)
            {
                var existing = await this.sponsoredListingReservationRepository
                    .GetReservationByGuidAsync(rsvId.Value)
                    .ConfigureAwait(false);

                if (existing is null ||
                    existing.ReservationGroup != reservationGroup ||
                    existing.ExpirationDateTime <= DateTime.UtcNow)
                {
                    rsvId = null; // treat as missing
                }
            }

            // New purchase path needs capacity & reservation; extensions do not
            if (!isExtension && !rsvId.HasValue)
            {
                var totalActiveListings = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(sponsorshipType, typeIdForCapacity)
                    .ConfigureAwait(false);

                var totalActiveReservations = await this.sponsoredListingReservationRepository
                    .GetActiveReservationsCountAsync(reservationGroup)
                    .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, sponsorshipType))
                {
                    // UPDATED: pass type, scoped typeId, group
                    var msg = await this.BuildCheckoutInProcessMessageAsync(sponsorshipType, typeIdForCapacity, reservationGroup);
                    return this.BadRequest(new { Error = msg });
                }

                var expiration = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var reservation = await this.sponsoredListingReservationRepository
                    .CreateReservationAsync(expiration, reservationGroup)
                    .ConfigureAwait(false);

                return this.RedirectToAction(
                    "SelectDuration",
                    new { directoryEntryId, sponsorshipType, rsvId = reservation.ReservationGuid });
            }

            // View context
            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.Subcategory = FormattingHelper.SubcategoryFormatting(
                    entry.SubCategory?.Category?.Name,
                    entry.SubCategory?.Name);
                this.ViewBag.SubCategoryId = entry.SubCategoryId;
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.Category = entry.SubCategory?.Category?.Name;
                this.ViewBag.CategoryId = entry.SubCategory?.CategoryId ?? 0;
            }

            this.ViewBag.DirectoryEntrName = entry.Name;
            this.ViewBag.DirectoryEntryId = entry.DirectoryEntryId;
            this.ViewBag.SponsorshipType = sponsorshipType;
            this.ViewBag.ReservationGuid = rsvId; // may be null for extensions

            var model = await this.GetListingDurations(sponsorshipType, entry.SubCategoryId)
                .ConfigureAwait(false);

            return this.View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
           int directoryEntryId,
           int selectedOfferId,
           Guid? rsvId = null)
        {
            var offer = await this.sponsoredListingOfferRepository
                .GetByIdAsync(selectedOfferId)
                .ConfigureAwait(false);
            if (offer is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var entry = await this.directoryEntryRepository
                .GetByIdAsync(directoryEntryId)
                .ConfigureAwait(false);
            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidListing });
            }

            var typeIdForGroup = offer.SponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                _ => 0,
            };
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeIdForGroup);

            int? typeIdForCapacity = offer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            var isExtension = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType)
                .ConfigureAwait(false);

            // Validate posted reservation
            if (rsvId.HasValue)
            {
                var existing = await this.sponsoredListingReservationRepository
                    .GetReservationByGuidAsync(rsvId.Value)
                    .ConfigureAwait(false);

                if (existing is null ||
                    existing.ReservationGroup != reservationGroup ||
                    existing.ExpirationDateTime <= DateTime.UtcNow)
                {
                    rsvId = null;
                }
            }

            // New purchase must (re)create reservation if needed; extensions do not
            if (!isExtension && !rsvId.HasValue)
            {
                var totalActiveListings = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(offer.SponsorshipType, typeIdForCapacity)
                    .ConfigureAwait(false);

                var totalActiveReservations = await this.sponsoredListingReservationRepository
                    .GetActiveReservationsCountAsync(reservationGroup)
                    .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, offer.SponsorshipType))
                {
                    // UPDATED: pass type, scoped typeId, group
                    var msg = await this.BuildCheckoutInProcessMessageAsync(offer.SponsorshipType, typeIdForCapacity, reservationGroup);
                    return this.BadRequest(new { Error = msg });
                }

                var expiration = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var newReservation = await this.sponsoredListingReservationRepository
                    .CreateReservationAsync(expiration, reservationGroup)
                    .ConfigureAwait(false);

                rsvId = newReservation.ReservationGuid;
            }

            return this.RedirectToAction(
                "ConfirmNowPayments",
                new { directoryEntryId, selectedOfferId, rsvId });
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/subcategoryselection")]
        public async Task<IActionResult> SubCategorySelection(
    int subCategoryId,
    Guid? rsvId = null)
        {
            const SponsorshipType type = SponsorshipType.SubcategorySponsor;
            var typeId = subCategoryId;
            var group = ReservationGroupHelper.BuildReservationGroupName(type, typeId);

            if (!await this.TryAttachReservationAsync(rsvId, group))
            {
                var totalActive = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(type, typeId).ConfigureAwait(false);
                var totalResv = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(group).ConfigureAwait(false);

                if (!CanPurchaseListing(totalActive, totalResv, type))
                {
                    // UPDATED: pass type, typeId, group
                    var msg = await this.BuildCheckoutInProcessMessageAsync(type, typeId, group);
                    return this.BadRequest(new { Error = msg });
                }

                var exp = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var res = await this.sponsoredListingReservationRepository.CreateReservationAsync(exp, group).ConfigureAwait(false);

                rsvId = res.ReservationGuid;
                await this.TryAttachReservationAsync(rsvId, group);

                return this.RedirectToAction(nameof(this.SubCategorySelection), new { subCategoryId, rsvId });
            }

            this.ViewBag.SubCategoryId = subCategoryId;

            var entries = await this.FilterEntries(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            return this.View("SubCategorySelection", entries);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/categoryselection")]
        public async Task<IActionResult> CategorySelection(
           int categoryId,
           Guid? rsvId = null)
        {
            const SponsorshipType type = SponsorshipType.CategorySponsor;
            var typeId = categoryId;
            var group = ReservationGroupHelper.BuildReservationGroupName(type, typeId);

            if (!await this.TryAttachReservationAsync(rsvId, group))
            {
                var totalActive = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(type, typeId).ConfigureAwait(false);
                var totalResv = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(group).ConfigureAwait(false);

                if (!CanPurchaseListing(totalActive, totalResv, type))
                {
                    // UPDATED: pass type, typeId, group
                    var msg = await this.BuildCheckoutInProcessMessageAsync(type, typeId, group);
                    return this.BadRequest(new { Error = msg });
                }

                var exp = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var res = await this.sponsoredListingReservationRepository.CreateReservationAsync(exp, group).ConfigureAwait(false);

                rsvId = res.ReservationGuid;
                await this.TryAttachReservationAsync(rsvId, group);

                return this.RedirectToAction(nameof(this.CategorySelection), new { categoryId, rsvId });
            }

            this.ViewBag.CategoryId = categoryId;

            var entries = await this.FilterEntries(SponsorshipType.CategorySponsor, categoryId).ConfigureAwait(false);
            return this.View("CategorySelection", entries);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmnowpayments")]
        public async Task<IActionResult> ConfirmNowPaymentsAsync(
            int directoryEntryId,
            int selectedOfferId,
            Guid? rsvId = null)
        {
            var offer = await this.sponsoredListingOfferRepository
                .GetByIdAsync(selectedOfferId)
                .ConfigureAwait(false);
            if (offer == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var entry = await this.directoryEntryRepository
                .GetByIdAsync(directoryEntryId)
                .ConfigureAwait(false);
            if (entry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            var typeIdForGroup = offer.SponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                _ => 0,
            };
            var group = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeIdForGroup);

            int? typeIdForCapacity = offer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            // Extension? If yes, skip reservation/capacity checks
            var isExtension = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType)
                .ConfigureAwait(false);

            if (!isExtension)
            {
                if (!await this.TryAttachReservationAsync(rsvId, group))
                {
                    var totalActive = await this.sponsoredListingRepository
                        .GetActiveSponsorsCountAsync(offer.SponsorshipType, typeIdForCapacity)
                        .ConfigureAwait(false);
                    var totalReservations = await this.sponsoredListingReservationRepository
                        .GetActiveReservationsCountAsync(group)
                        .ConfigureAwait(false);

                    if (!CanPurchaseListing(totalActive, totalReservations, offer.SponsorshipType))
                    {
                        // UPDATED: pass type, scoped typeId, group
                        var msg = await this.BuildCheckoutInProcessMessageAsync(offer.SponsorshipType, typeIdForCapacity, group);
                        return this.BadRequest(new { Error = msg });
                    }
                }
            }

            if (offer.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.SubCategoryId = typeIdForGroup;
            }
            else if (offer.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.CategoryId = typeIdForGroup;
            }

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);
            var current = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(offer.SponsorshipType)
                .ConfigureAwait(false);

            var vm = GetConfirmationModel(offer, entry, link2Name, link3Name, current);
            vm.CanCreateSponsoredListing = true; // always true for extension; for new, we’re here only if allowed

            return this.View(vm);
        }
        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmnowpayments")]
        public async Task<IActionResult> ConfirmedNowPaymentsAsync(
           int directoryEntryId,
           int selectedOfferId,
           Guid? rsvId = null,
           string? email = null)
        {
            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.NotFound();
            }

            var sponsoredListingOffer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);
            if (sponsoredListingOffer == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (directoryEntry == null)
            {
                return this.BadRequest(new { Error = StringConstants.DirectoryEntryNotFound });
            }

            var typeIdForGroup = sponsoredListingOffer.SponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => directoryEntry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => directoryEntry.SubCategoryId,
                _ => 0,
            };
            var group = ReservationGroupHelper.BuildReservationGroupName(sponsoredListingOffer.SponsorshipType, typeIdForGroup);

            int? typeIdForCapacity = sponsoredListingOffer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            // Exempt holder of a valid reservation; otherwise enforce capacity
            if (!await this.TryAttachReservationAsync(rsvId, group))
            {
                var isActiveSponsor = await this.sponsoredListingRepository
                    .IsSponsoredListingActive(directoryEntryId, sponsoredListingOffer.SponsorshipType);

                var totalActiveListings = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(sponsoredListingOffer.SponsorshipType, typeIdForCapacity);

                var totalActiveReservations = await this.sponsoredListingReservationRepository
                    .GetActiveReservationsCountAsync(group);

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, sponsoredListingOffer.SponsorshipType) && !isActiveSponsor)
                {
                    // UPDATED: pass type, scoped typeId, group
                    var msg = await this.BuildCheckoutInProcessMessageAsync(sponsoredListingOffer.SponsorshipType, typeIdForCapacity, group);
                    return this.BadRequest(new { Error = msg });
                }
            }
            else
            {
                // We have a valid reservation — supersede unpaid prior invoice for this reservation
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);
                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }

                var existingInvoice = await this.sponsoredListingInvoiceRepository.GetByReservationGuidAsync(existingReservation.ReservationGuid);
                if (existingInvoice != null)
                {
                    if (existingInvoice.PaymentStatus == PaymentStatus.Paid)
                    {
                        return this.BadRequest(new { Error = StringConstants.InvoiceAlreadyCreated });
                    }

                    existingInvoice.PaymentStatus = PaymentStatus.Canceled;
                    existingInvoice.ReservationGuid = Guid.Empty;
                    await this.sponsoredListingInvoiceRepository.UpdateAsync(existingInvoice);
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            var existingListing = await this.sponsoredListingRepository
                .GetActiveSponsorAsync(directoryEntryId, sponsoredListingOffer.SponsorshipType);

            var startDate = existingListing?.CampaignEndDate ?? DateTime.UtcNow;

            var invoice = await this.CreateInvoice(directoryEntry, sponsoredListingOffer, startDate, ipAddress);
            var invoiceRequest = this.GetInvoiceRequest(sponsoredListingOffer, invoice);

            this.paymentService.SetDefaultUrls(invoiceRequest);

            var invoiceFromProcessor = await this.paymentService.CreateInvoice(invoiceRequest);
            if (invoiceFromProcessor == null || invoiceFromProcessor.Id == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice." });
            }

            SetInvoiceProperties(rsvId, email, invoice, invoiceRequest, invoiceFromProcessor);
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            if (string.IsNullOrWhiteSpace(invoiceFromProcessor.InvoiceUrl))
            {
                return this.BadRequest(new { Error = "Failed to get invoice URL." });
            }

            return this.Redirect(invoiceFromProcessor.InvoiceUrl);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentscallback")]
        public async Task<IActionResult> NowPaymentsCallBackAsync()
        {
            string callbackPayload;
            using (var reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                callbackPayload = await reader.ReadToEndAsync();
            }

            // Log at info (or debug) level rather than error to avoid noise
            this.logger.LogInformation("NOWPayments IPN received: {Payload}", callbackPayload);

            // 1) Verify signature FIRST — if invalid, acknowledge anyway (nothing to retry).
            var nowPaymentsSig = this.Request
                .Headers[NowPayments.API.Constants.StringConstants.HeaderNameAuthCallBack]
                .FirstOrDefault() ?? string.Empty;

            if (!this.paymentService.IsIpnRequestValid(callbackPayload, nowPaymentsSig, out var sigError))
            {
                this.logger.LogWarning("NOWPayments IPN signature invalid: {Error}", sigError);
                return this.Ok(); // acknowledge to prevent retries
            }

            // 2) Parse payload safely
            IpnPaymentMessage? ipnMessage = null;
            try
            {
                ipnMessage = JsonConvert.DeserializeObject<IpnPaymentMessage>(callbackPayload);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "NOWPayments IPN deserialize failed.");
                return this.Ok(); // acknowledge; we can’t process this one
            }

            if (ipnMessage?.OrderId == null || ipnMessage.PaymentStatus == null)
            {
                this.logger.LogWarning("NOWPayments IPN missing OrderId or PaymentStatus. Payload: {Payload}", callbackPayload);
                return this.Ok();
            }

            // 3) Find our invoice
            var orderId = ipnMessage.OrderId;
            var invoice = await this.sponsoredListingInvoiceRepository.GetByInvoiceIdAsync(Guid.Parse(orderId));
            if (invoice == null)
            {
                // Could be a stray/old callback; log and ack
                this.logger.LogWarning("NOWPayments IPN for unknown OrderId: {OrderId}", orderId);
                return this.Ok();
            }

            // 4) Idempotency: if already terminal, we’re done
            if (invoice.PaymentStatus is PaymentStatus.Paid
                or PaymentStatus.Expired
                or PaymentStatus.Failed
                or PaymentStatus.Refunded
                or PaymentStatus.Canceled)
            {
                return this.Ok();
            }

            // 5) Update invoice with latest gateway info
            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.PaidAmount = ipnMessage.PayAmount;
            invoice.OutcomeAmount = ipnMessage.OutcomeAmount;

            // Map external status to internal; do not downgrade a Paid invoice
            var processorStatus = DirectoryManager.Utilities.Helpers.EnumHelper
                .ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(ipnMessage.PaymentStatus);

            var newStatus = ConvertToInternalStatus(processorStatus);
            if (invoice.PaymentStatus != PaymentStatus.Paid)
            {
                invoice.PaymentStatus = newStatus;
            }

            if (!string.IsNullOrWhiteSpace(ipnMessage.PayCurrency))
            {
                invoice.PaidInCurrency = DirectoryManager.Utilities.Helpers.EnumHelper
                    .ParseStringToEnum<Currency>(ipnMessage.PayCurrency);
            }

            // 6) Apply business effects (creates/extends listing when Paid) — your method is already idempotent
            await this.CreateNewSponsoredListing(invoice);

            // 7) Always 200 OK so NOWPayments doesn’t retry
            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentssuccess")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "External parameter name.")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);
            if (processorInvoice == null || processorInvoice.OrderId == null)
            {
                return this.BadRequest(new { Error = DirectoryManager.Web.Constants.StringConstants.InvoiceNotFound });
            }

            var existingInvoice = await this.sponsoredListingInvoiceRepository
                .GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId))
                .ConfigureAwait(false);

            if (existingInvoice == null)
            {
                return this.BadRequest(new { Error = DirectoryManager.Web.Constants.StringConstants.InvoiceNotFound });
            }

            // Do NOT resurrect superseded invoices
            if (existingInvoice.PaymentStatus == PaymentStatus.Canceled)
            {
                // Persist any response for audit, but don't modify listing or status.
                existingInvoice.PaymentResponse = NP_id;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(existingInvoice);

                var vmCanceled = new SuccessViewModel
                {
                    OrderId = existingInvoice.InvoiceId,
                    ListingEndDate = existingInvoice.CampaignEndDate
                };
                return this.View("NowPaymentsSuccess", vmCanceled);
            }

            // Idempotent: don’t downgrade; mark paid if not already
            if (existingInvoice.PaymentStatus != PaymentStatus.Paid)
            {
                existingInvoice.PaymentStatus = PaymentStatus.Paid;
                existingInvoice.PaymentResponse = NP_id;
            }

            await this.CreateNewSponsoredListing(existingInvoice);

            var viewModel = new SuccessViewModel
            {
                OrderId = existingInvoice.InvoiceId,
                ListingEndDate = existingInvoice.CampaignEndDate
            };

            return this.View("NowPaymentsSuccess", viewModel);
        }

        [Route("sponsoredlisting/edit/{sponsoredListingId}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditAsync(int sponsoredListingId)
        {
            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId);
            if (listing == null)
            {
                return this.NotFound();
            }

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(listing.DirectoryEntryId);

            var model = new EditListingViewModel
            {
                Id = listing.SponsoredListingId,
                CampaignStartDate = listing.CampaignStartDate,
                CampaignEndDate = listing.CampaignEndDate,
                SponsorshipType = listing.SponsorshipType,
                Name = directoryEntry.Name,
            };

            return this.View(model);
        }

        [Route("sponsoredlisting/edit/{sponsoredListingId}")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditAsync(int sponsoredListingId, EditListingViewModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId);
            if (listing == null)
            {
                return this.NotFound();
            }

            listing.CampaignStartDate = model.CampaignStartDate;
            listing.CampaignEndDate = model.CampaignEndDate;

            await this.sponsoredListingRepository.UpdateAsync(listing);

            this.cache.Remove(StringConstants.CacheKeyEntries);
            this.cache.Remove(StringConstants.CacheKeySponsoredListings);

            return this.RedirectToAction("List");
        }

        [AllowAnonymous]
        [Route("sponsoredlisting/offers")]
        [HttpGet]
        public async Task<IActionResult> Offers()
        {
            var mainOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.MainSponsor);
            var categoryOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.CategorySponsor);
            var subcategoryOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.SubcategorySponsor);

            var mainActiveCount = (await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)).Count();
            var activeByCategory = await this.sponsoredListingRepository.GetActiveSponsorCountByCategoryAsync(SponsorshipType.CategorySponsor);
            var activeBySubcategory = await this.sponsoredListingRepository.GetActiveSponsorCountBySubcategoryAsync(SponsorshipType.SubcategorySponsor);

            var allCats = await this.categoryRepository.GetActiveCategoriesAsync();
            var allSubcats = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync();

            var freeCategories = allCats.Where(c => !activeByCategory.ContainsKey(c.CategoryId)).Select(c => c.CategoryId).ToHashSet();
            var freeSubcategories = allSubcats.Where(sc => !activeBySubcategory.ContainsKey(sc.SubCategoryId)).Select(sc => sc.SubCategoryId).ToHashSet();

            var vmMain = mainOffers
                .Select(o =>
                {
                    var available = mainActiveCount < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
                    var link = available
                        ? this.Url.Action("SelectListing", "SponsoredListing", new { sponsorshipType = SponsorshipType.MainSponsor })
                        : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.MainSponsor });

                    var label = o.Subcategory != null
                        ? FormattingHelper.SubcategoryFormatting(o.Subcategory.Category!.Name, o.Subcategory.Name)
                        : StringConstants.Default;

                    return new SponsoredListingOfferDisplayModel
                    {
                        Description = o.Description,
                        Days = o.Days,
                        Price = o.Price,
                        PriceCurrency = o.PriceCurrency,
                        SponsorshipType = o.SponsorshipType,
                        CategorySubcategory = label,
                        IsAvailable = available,
                        ActionLink = link,
                    };
                })
                .OrderBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

            var vmCat = categoryOffers
                .Select(o =>
                {
                    var isDefault = o.Subcategory == null;
                    var catId = o.Subcategory?.CategoryId ?? 0;
                    var available = isDefault || freeCategories.Contains(catId);

                    string? link;
                    if (available)
                    {
                        link = isDefault
                            ? this.Url.Action("SelectListing", "SponsoredListing", new { sponsorshipType = SponsorshipType.CategorySponsor })
                            : this.Url.Action("SelectListing", "SponsoredListing", new { sponsorshipType = SponsorshipType.CategorySponsor, categoryId = catId });
                    }
                    else
                    {
                        link = this.Url.Action("Subscribe", "SponsoredListingNotification", new
                        {
                            sponsorshipType = SponsorshipType.CategorySponsor,
                            typeId = isDefault ? (int?)null : catId,
                        });
                    }

                    var label = isDefault
                        ? StringConstants.Default
                        : FormattingHelper.SubcategoryFormatting(o.Subcategory.Category.Name, o.Subcategory.Name);

                    return new SponsoredListingOfferDisplayModel
                    {
                        Description = o.Description,
                        Days = o.Days,
                        Price = o.Price,
                        PriceCurrency = o.PriceCurrency,
                        SponsorshipType = o.SponsorshipType,
                        CategorySubcategory = label,
                        IsAvailable = available,
                        ActionLink = link,
                    };
                })
                .OrderBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

            var vmSub = subcategoryOffers
                .Select(o =>
                {
                    var isDefault = o.Subcategory == null;
                    var subId = o.Subcategory?.SubCategoryId ?? 0;
                    var available = isDefault || freeSubcategories.Contains(subId);

                    string? link;
                    if (available)
                    {
                        link = isDefault
                            ? this.Url.Action("SelectListing", "SponsoredListing", new { sponsorshipType = SponsorshipType.SubcategorySponsor })
                            : this.Url.Action("SelectListing", "SponsoredListing", new { sponsorshipType = SponsorshipType.SubcategorySponsor, subCategoryId = subId });
                    }
                    else
                    {
                        link = this.Url.Action("Subscribe", "SponsoredListingNotification", new
                        {
                            sponsorshipType = SponsorshipType.SubcategorySponsor,
                            typeId = isDefault ? (int?)null : subId,
                        });
                    }

                    var label = isDefault
                        ? StringConstants.Default
                        : FormattingHelper.SubcategoryFormatting(o.Subcategory.Category.Name, o.Subcategory.Name);

                    return new SponsoredListingOfferDisplayModel
                    {
                        Description = o.Description,
                        Days = o.Days,
                        Price = o.Price,
                        PriceCurrency = o.PriceCurrency,
                        SponsorshipType = o.SponsorshipType,
                        CategorySubcategory = label,
                        IsAvailable = available,
                        ActionLink = link,
                    };
                })
                .OrderBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

            var lastUpdated = await this.sponsoredListingOfferRepository.GetLastModifiedDateAsync();

            var model = new SponsoredListingOffersViewModel
            {
                MainSponsorshipOffers = vmMain,
                CategorySponsorshipOffers = vmCat,
                SubCategorySponsorshipOffers = vmSub,
                ConversionRate = 0,
                SelectedCurrency = "XMR",
                LastUpdatedDate = lastUpdated,
            };

            return this.View(model);
        }

        [AllowAnonymous]
        [Route("sponsoredlisting/current")]
        [HttpGet]
        public IActionResult Current()
        {
            return this.View();
        }

        [Route("sponsoredlisting/activelistings")]
        [HttpGet]
        public async Task<IActionResult> ActiveListings()
        {
            var listings = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();

            var mainSponsorListings = listings.Where(l => l.SponsorshipType == SponsorshipType.MainSponsor).ToList();
            var subCategorySponsorListings = listings.Where(l => l.SponsorshipType == SponsorshipType.SubcategorySponsor).ToList();
            var categorySponsorListings = listings.Where(l => l.SponsorshipType == SponsorshipType.CategorySponsor).ToList();

            var model = new ActiveSponsoredListingViewModel
            {
                MainSponsorItems = mainSponsorListings.Select(listing => new ActiveSponsoredListingModel
                {
                    ListingName = listing.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = listing.SponsoredListingId,
                    CampaignEndDate = listing.CampaignEndDate,
                    ListingUrl = listing.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = listing.DirectoryEntryId,
                    SponsorshipType = listing.SponsorshipType,
                }).ToList(),

                SubCategorySponsorItems = subCategorySponsorListings.Select(listing => new ActiveSponsoredListingModel
                {
                    ListingName = listing.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = listing.SponsoredListingId,
                    CampaignEndDate = listing.CampaignEndDate,
                    ListingUrl = listing.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = listing.DirectoryEntryId,
                    SubcategoryName = this.SetSubcategoryNameAsync(listing.SubCategoryId),
                    SponsorshipType = listing.SponsorshipType,
                }).ToList(),

                CategorySponsorItems = categorySponsorListings.Select(listing => new ActiveSponsoredListingModel
                {
                    ListingName = listing.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = listing.SponsoredListingId,
                    CampaignEndDate = listing.CampaignEndDate,
                    ListingUrl = listing.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = listing.DirectoryEntryId,
                    CategoryName = this.CategoryNameAsync(listing.CategoryId),
                    SponsorshipType = listing.SponsorshipType,
                }).ToList(),
            };

            return this.View("activelistings", model);
        }

        [Route("sponsoredlisting/list/{page?}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> List(int page = 1)
        {
            var pageSize = IntegerConstants.DefaultPageSize;
            var totalListings = await this.sponsoredListingRepository.GetTotalCountAsync();
            var listings = await this.sponsoredListingRepository.GetPaginatedListingsAsync(page, pageSize);

            var model = new PaginatedListingsViewModel
            {
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalListings / (double)pageSize),
                Listings = listings.Select(l => new ListingViewModel
                {
                    Id = l.SponsoredListingId,
                    DirectoryEntryName = l.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsorshipType = l.SponsorshipType,
                    StartDate = l.CampaignStartDate,
                    EndDate = l.CampaignEndDate,
                }).ToList(),
            };

            return this.View(model);
        }

        private static int GetMaxSlotsForType(SponsorshipType type)
        {
            return type switch
            {
                SponsorshipType.MainSponsor => Common.Constants.IntegerConstants.MaxMainSponsoredListings,
                SponsorshipType.CategorySponsor => Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
                SponsorshipType.SubcategorySponsor => Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
                _ => 0
            };
        }

        private static ConfirmSelectionViewModel GetConfirmationModel(
            SponsoredListingOffer offer,
            DirectoryEntry directoryEntry,
            string link2Name,
            string link3Name,
            IEnumerable<SponsoredListing> currentListings)
        {
            return new ConfirmSelectionViewModel
            {
                SelectedDirectoryEntry = new DirectoryEntryViewModel
                {
                    CreateDate = directoryEntry.CreateDate,
                    UpdateDate = directoryEntry.UpdateDate,
                    ItemDisplayType = DisplayFormatting.Enums.ItemDisplayType.Normal,
                    DateOption = DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                    IsSponsored = false,
                    Link2Name = link2Name,
                    Link3Name = link3Name,
                    Link = directoryEntry.Link,
                    Name = directoryEntry.Name,
                    DirectoryEntryKey = directoryEntry.DirectoryEntryKey,
                    Contact = directoryEntry.Contact,
                    Description = directoryEntry.Description,
                    DirectoryEntryId = directoryEntry.DirectoryEntryId,
                    DirectoryStatus = directoryEntry.DirectoryStatus,
                    Link2 = directoryEntry.Link2,
                    Link3 = directoryEntry.Link3,
                    Location = directoryEntry.Location,
                    Note = directoryEntry.Note,
                    Processor = directoryEntry.Processor,
                    SubCategoryId = directoryEntry.SubCategoryId,
                    CountryCode = directoryEntry.CountryCode,
                    PgpKey = directoryEntry.PgpKey,
                },
                Offer = new SponsoredListingOfferModel
                {
                    Description = offer.Description,
                    Days = offer.Days,
                    SponsoredListingOfferId = offer.SponsoredListingOfferId,
                    USDPrice = offer.Price,
                    SponsorshipType = offer.SponsorshipType,
                },
                IsExtension = currentListings.FirstOrDefault(x => x.DirectoryEntryId == directoryEntry.DirectoryEntryId) != null,
            };
        }

        private static void SetInvoiceProperties(
            Guid? rsvId,
            string? email,
            SponsoredListingInvoice invoice,
            PaymentRequest invoiceRequest,
            InvoiceResponse invoiceFromProcessor)
        {
            invoice.ReservationGuid = rsvId == null ? Guid.Empty : rsvId.Value;
            invoice.ProcessorInvoiceId = invoiceFromProcessor.Id;
            invoice.PaymentProcessor = PaymentProcessor.NOWPayments;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(invoiceRequest);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(invoiceFromProcessor);
            invoice.Email = InputHelper.SetEmail(email);
        }

        private static PaymentStatus ConvertToInternalStatus(NowPayments.API.Enums.PaymentStatus externalStatus)
        {
            return externalStatus switch
            {
                NowPayments.API.Enums.PaymentStatus.Unknown => PaymentStatus.Unknown,
                NowPayments.API.Enums.PaymentStatus.Waiting => PaymentStatus.InvoiceCreated,
                NowPayments.API.Enums.PaymentStatus.Sending or
                NowPayments.API.Enums.PaymentStatus.Confirming or
                NowPayments.API.Enums.PaymentStatus.Confirmed => PaymentStatus.Pending,
                NowPayments.API.Enums.PaymentStatus.Finished => PaymentStatus.Paid,
                NowPayments.API.Enums.PaymentStatus.PartiallyPaid => PaymentStatus.UnderPayment,
                NowPayments.API.Enums.PaymentStatus.Failed or
                NowPayments.API.Enums.PaymentStatus.Refunded => PaymentStatus.Failed,
                NowPayments.API.Enums.PaymentStatus.Expired => PaymentStatus.Expired,
                _ => throw new ArgumentOutOfRangeException(nameof(externalStatus), externalStatus, null),
            };
        }

        private static int? GetSponsorshipTypeId(SponsoredListingOffer selectedOffer, DirectoryEntry directoryEntry)
        {
            switch (selectedOffer.SponsorshipType)
            {
                case SponsorshipType.MainSponsor:
                    return 0;
                case SponsorshipType.SubcategorySponsor:
                    return directoryEntry?.SubCategoryId;
                case SponsorshipType.CategorySponsor:
                    return directoryEntry?.SubCategory?.CategoryId;
                default:
                    throw new Exception("Uknown type for offer");
            }
        }

        private static bool CanAdvertise(SponsorshipType sponsorshipType, int totalForTypeInGroup)
        {
            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                return totalForTypeInGroup < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
            }

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                return totalForTypeInGroup < Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings;
            }

            if (sponsorshipType == SponsorshipType.CategorySponsor)
            {
                return totalForTypeInGroup < Common.Constants.IntegerConstants.MaxCategorySponsoredListings;
            }

            throw new InvalidOperationException("SponsorshipType:" + sponsorshipType.ToString());
        }

        private static bool CanPurchaseListing(
            int totalActiveListings,
            int totalActiveReservations,
            SponsorshipType sponsorshipType)
        {
            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                return (totalActiveListings <= Common.Constants.IntegerConstants.MaxMainSponsoredListings) &&
                       (totalActiveReservations < (Common.Constants.IntegerConstants.MaxMainSponsoredListings - totalActiveListings));
            }

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                return (totalActiveListings <= Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings) &&
                       (totalActiveReservations < (Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings - totalActiveListings));
            }

            if (sponsorshipType == SponsorshipType.CategorySponsor)
            {
                return (totalActiveListings <= Common.Constants.IntegerConstants.MaxCategorySponsoredListings) &&
                       (totalActiveReservations < (Common.Constants.IntegerConstants.MaxCategorySponsoredListings - totalActiveListings));
            }

            throw new NotImplementedException("SponsorshipType:" + sponsorshipType.ToString());
        }

        private string SetSubcategoryNameAsync(int? subCategoryId)
        {
            if (subCategoryId == null)
            {
                return string.Empty;
            }

            var subcategory = this.subCategoryRepository.GetByIdAsync(subCategoryId.Value).Result;
            if (subcategory == null)
            {
                return string.Empty;
            }

            var category = this.categoryRepository.GetByIdAsync(subcategory.CategoryId).Result;
            if (category == null)
            {
                return string.Empty;
            }

            return FormattingHelper.SubcategoryFormatting(category.Name, subcategory.Name);
        }

        private string CategoryNameAsync(int? categoryId)
        {
            if (categoryId == null)
            {
                return string.Empty;
            }

            var category = this.categoryRepository.GetByIdAsync(categoryId.Value).Result;
            if (category == null)
            {
                return string.Empty;
            }

            return category.Name;
        }

        private PaymentRequest GetInvoiceRequest(
            SponsoredListingOffer sponsoredListingOffer,
            SponsoredListingInvoice invoice)
        {
            return new PaymentRequest
            {
                IsFeePaidByUser = false,
                PriceAmount = sponsoredListingOffer.Price,
                PriceCurrency = this.paymentService.PriceCurrency,
                PayCurrency = this.paymentService.PayCurrency,
                OrderId = invoice.InvoiceId.ToString(),
                OrderDescription = sponsoredListingOffer.Description,
            };
        }

        private async Task<SponsoredListingInvoice> CreateInvoice(
            DirectoryEntry directoryEntry,
            SponsoredListingOffer sponsoredListingOffer,
            DateTime startDate,
            string ipAddress)
        {
            if (!this.HttpContext.ShouldLogIp())
            {
                ipAddress = string.Empty;
            }

            return await this.sponsoredListingInvoiceRepository.CreateAsync(
                new SponsoredListingInvoice
                {
                    DirectoryEntryId = directoryEntry.DirectoryEntryId,
                    Currency = Currency.USD,
                    InvoiceId = Guid.NewGuid(),
                    PaymentStatus = PaymentStatus.InvoiceCreated,
                    CampaignStartDate = startDate,
                    CampaignEndDate = startDate.AddDays(sponsoredListingOffer.Days),
                    Amount = sponsoredListingOffer.Price,
                    InvoiceDescription = sponsoredListingOffer.Description,
                    SponsorshipType = sponsoredListingOffer.SponsorshipType,
                    SubCategoryId = directoryEntry.SubCategoryId,
                    CategoryId = directoryEntry?.SubCategory?.CategoryId,
                    IpAddress = ipAddress,
                });
        }

        private async Task<List<SponsoredListingOfferModel>> GetListingDurations(SponsorshipType sponsorshipType, int? subcategoryId)
        {
            var offers = await this.sponsoredListingOfferRepository.GetByTypeAndSubCategoryAsync(sponsorshipType, subcategoryId);
            var model = new List<SponsoredListingOfferModel>();

            foreach (var offer in offers.OrderBy(x => x.Days))
            {
                model.Add(new SponsoredListingOfferModel
                {
                    SponsoredListingOfferId = offer.SponsoredListingOfferId,
                    Description = offer.Description,
                    Days = offer.Days,
                    USDPrice = offer.Price,
                });
            }

            return model;
        }

        private async Task CreateNewSponsoredListing(SponsoredListingInvoice invoice)
        {
            // Persist any updated invoice fields first
            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                return;
            }

            // Only apply to listings if this invoice is PAID
            if (invoice.PaymentStatus != PaymentStatus.Paid)
            {
                return;
            }

            // If a listing is already tied to this invoice, nothing else to do
            var listingByInvoice = await this.sponsoredListingRepository
                .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId)
                .ConfigureAwait(false);

            if (listingByInvoice != null)
            {
                return;
            }

            // Find the active listing for this entry/type
            var activeListing = await this.sponsoredListingRepository
                .GetActiveSponsorAsync(invoice.DirectoryEntryId, invoice.SponsorshipType)
                .ConfigureAwait(false);

            if (activeListing == null)
            {
                // No active listing → create new
                var created = await this.sponsoredListingRepository.CreateAsync(
                    new SponsoredListing
                    {
                        DirectoryEntryId = invoice.DirectoryEntryId,
                        CampaignStartDate = invoice.CampaignStartDate,
                        CampaignEndDate = invoice.CampaignEndDate,
                        SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                        SponsorshipType = invoice.SponsorshipType,
                        SubCategoryId = invoice.SubCategoryId,
                        CategoryId = invoice.CategoryId,
                    });

                invoice.SponsoredListingId = created.SponsoredListingId;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
                this.ClearCachedItems();
                return;
            }

            // Active listing exists → EXTEND, but never shorten
            var proposedEnd = invoice.CampaignEndDate;
            if (proposedEnd > activeListing.CampaignEndDate)
            {
                activeListing.CampaignEndDate = proposedEnd;
                activeListing.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId;
                await this.sponsoredListingRepository.UpdateAsync(activeListing);
            }

            // Always link the paid invoice to the listing for bookkeeping
            invoice.SponsoredListingId = activeListing.SponsoredListingId;
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            this.ClearCachedItems();
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntries(int? subCategoryId, int? categoryId)
        {
            var entries = await this.directoryEntryRepository.GetAllowableAdvertisers();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategoryId == subCategoryId.Value).ToList();
            }
            else if (categoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory.CategoryId == categoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name).ToList();
            await this.GetSubCateogryOptions();

            return entries;
        }

        private async Task GetSubCateogryOptions()
        {
            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync())
                .OrderBy(sc => sc.Category.Name)
                .ThenBy(sc => sc.Name)
                .ToList();
        }

        private bool IsOldEnough(DirectoryEntry directoryEntry)
        {
            if (directoryEntry.CreateDate == DateTime.MinValue)
            {
                return false;
            }

            if (directoryEntry.DirectoryStatus == DirectoryStatus.Verified)
            {
                return true;
            }

            return (DateTime.UtcNow - directoryEntry.CreateDate).TotalDays >= IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising;
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntries(SponsorshipType sponsorshipType, int? typeId)
        {
            var entries = await this.directoryEntryRepository.GetAllowableAdvertisers().ConfigureAwait(false);

            if (sponsorshipType == SponsorshipType.SubcategorySponsor && typeId.HasValue)
            {
                entries = entries.Where(e => e.SubCategoryId == typeId.Value).ToList();
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor && typeId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory != null && e.SubCategory.CategoryId == typeId.Value).ToList();
            }

            return entries.OrderBy(e => e.Name).ToList();
        }

        /// <summary>
        /// If <paramref name="rsvId"/> is non-null and valid for <paramref name="reservationGroup"/>,
        /// attach it to ViewBag and return true. Also exposes the reservation's expiration for banners.
        /// </summary>
        private async Task<bool> TryAttachReservationAsync(Guid? rsvId, string reservationGroup)
        {
            if (rsvId == null)
            {
                return false;
            }

            var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value).ConfigureAwait(false);
            if (existing == null)
            {
                return false;
            }

            // best-effort: read expiration either from entity or from group helper as fallback
            DateTime? expiresUtc = null;
            try
            {
                // assuming repository entity exposes ExpirationDate (rename if different)
                var prop = existing.GetType().GetProperty("ExpirationDate");
                expiresUtc = (DateTime?)prop?.GetValue(existing);
            }
            catch
            {
                // ignore and fallback
            }

            if (!expiresUtc.HasValue)
            {
                expiresUtc = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(reservationGroup).ConfigureAwait(false);
            }

            // If expired or mismatched group, reject.
            if (!expiresUtc.HasValue || expiresUtc.Value <= DateTime.UtcNow)
            {
                return false;
            }

            this.ViewBag.ReservationGuid = rsvId;
            this.ViewBag.ReservationExpiresUtc = expiresUtc.Value;
            return true;
        }

        // Helper to name the exact scope we’re talking about (site / category / subcategory)
        private async Task<string> GetScopeLabelAsync(SponsorshipType sponsorshipType, int? typeId)
        {
            switch (sponsorshipType)
            {
                case SponsorshipType.MainSponsor:
                    return "Main Sponsor";

                case SponsorshipType.CategorySponsor:
                    if (typeId.HasValue)
                    {
                        var cat = await this.categoryRepository.GetByIdAsync(typeId.Value).ConfigureAwait(false);
                        if (cat != null) return $"category \"{cat.Name}\"";
                    }
                    return "the selected category";

                case SponsorshipType.SubcategorySponsor:
                    if (typeId.HasValue)
                    {
                        var sub = await this.subCategoryRepository.GetByIdAsync(typeId.Value).ConfigureAwait(false);
                        if (sub != null)
                        {
                            var cat = sub.Category ?? await this.categoryRepository.GetByIdAsync(sub.CategoryId).ConfigureAwait(false);
                            var catName = cat?.Name ?? "Unknown Category";
                            return $"subcategory \"{catName} > {sub.Name}\"";
                        }
                    }
                    return "the selected subcategory";

                default:
                    return "this selection";
            }
        }

        /// <summary>
        /// Returns a user-friendly block message explaining *why* purchase can't proceed,
        /// with scope-aware wording (Main vs specific Category/Subcategory).
        /// </summary>
        private async Task<string> BuildCheckoutInProcessMessageAsync(
            SponsorshipType sponsorshipType,
            int? typeId,
            string reservationGroup)
        {
            var scope = await this.GetScopeLabelAsync(sponsorshipType, typeId).ConfigureAwait(false);

            // 1) Capacity check (scoped by type/typeId)
            var max = GetMaxSlotsForType(sponsorshipType);
            var totalActiveListings = await this.sponsoredListingRepository
                .GetActiveSponsorsCountAsync(sponsorshipType, typeId)
                .ConfigureAwait(false);

            if (totalActiveListings >= max)
            {
                var next = await this.GetNextOpeningUtcAsync(sponsorshipType, typeId).ConfigureAwait(false);
                if (next.HasValue)
                {
                    return $"No ad space available right now for {scope}. Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC.";
                }

                return $"No ad space available right now for {scope}.";
            }

            // 2) Otherwise capacity exists, so the block is a live reservation
            var expiration = await this.sponsoredListingReservationRepository
                .GetActiveReservationExpirationAsync(reservationGroup)
                .ConfigureAwait(false);

            if (expiration.HasValue)
            {
                var expiryUtc = expiration.Value;
                var minutesLeft = Math.Max(1, (int)Math.Ceiling((expiryUtc - DateTime.UtcNow).TotalMinutes));
                return $"Another checkout for {scope} is in process and will expire at {expiryUtc:yyyy-MM-dd HH:mm} UTC (in {minutesLeft} minutes).";
            }

            // Fallback (should be rare)
            return $"Another checkout is currently in process for {scope}.";
        }

        private async Task<DateTime?> GetNextOpeningUtcAsync(SponsorshipType type, int? typeId)
        {
            var all = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(type)
                .ConfigureAwait(false);

            IEnumerable<SponsoredListing> scoped = type switch
            {
                SponsorshipType.MainSponsor => all, // one shared pool
                SponsorshipType.CategorySponsor => all.Where(x => x.CategoryId == (typeId ?? 0)),
                SponsorshipType.SubcategorySponsor => all.Where(x => x.SubCategoryId == typeId),
                _ => Enumerable.Empty<SponsoredListing>()
            };

            return scoped.Any() ? scoped.Min(x => (DateTime?)x.CampaignEndDate) : null;
        }
    }
}