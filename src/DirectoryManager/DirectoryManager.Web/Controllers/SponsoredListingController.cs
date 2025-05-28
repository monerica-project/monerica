using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
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
            var currentMainSponsorListings = await this.sponsoredListingRepository
                                                      .GetActiveSponsorsByTypeAsync(mainSponsorType);
            var model = new SponsoredListingHomeModel();

            // Main sponsor logic
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
                    var totalActiveListings = await this.sponsoredListingRepository
                                                       .GetActiveSponsorsCountAsync(mainSponsorType, null);
                    var totalActiveReservations = await this.sponsoredListingReservationRepository
                                                           .GetActiveReservationsCountAsync(mainSponsorReservationGroup);

                    if (CanPurchaseListing(totalActiveListings, totalActiveReservations, mainSponsorType))
                    {
                        model.CanCreateMainListing = true;
                    }
                    else
                    {
                        model.Message = StringConstants.CheckoutInProcess;
                        model.CanCreateMainListing = false;
                    }
                }

                model.NextListingExpiration = currentMainSponsorListings.Min(x => x.CampaignEndDate);
            }
            else
            {
                model.CanCreateMainListing = true;
            }

            // Subcategory sponsor logic
            var allActiveSubcategories = await this.subCategoryRepository
                                              .GetAllActiveSubCategoriesAsync(Common.Constants.IntegerConstants.MinimumSponsoredActiveSubcategories);
            var currentSubSponsors = await this.sponsoredListingRepository
                                       .GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);

            if (currentSubSponsors != null)
            {
                foreach (var sc in allActiveSubcategories)
                {
                    var label = FormattingHelper.SubcategoryFormatting(sc.Category.Name, sc.Name);
                    if (currentSubSponsors.Any(x => x.SubCategoryId == sc.SubCategoryId))
                        model.UnavailableSubCatetgories.Add(sc.SubCategoryId, label);
                    else
                        model.AvailableSubCatetgories.Add(sc.SubCategoryId, label);
                }

                model.AvailableSubCatetgories = model.AvailableSubCatetgories.OrderBy(kv => kv.Value)
                                                                                  .ToDictionary(kv => kv.Key, kv => kv.Value);
                model.UnavailableSubCatetgories = model.UnavailableSubCatetgories.OrderBy(kv => kv.Value)
                                                                                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            // Category sponsor logic
            var allCategories = await this.categoryRepository.GetAllAsync();
            var currentCatSponsors = await this.sponsoredListingRepository
                                       .GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);

            if (currentCatSponsors != null)
            {
                foreach (var cat in allCategories)
                {
                    var label = cat.Name;
                    if (currentCatSponsors.Any(x =>
                            x.DirectoryEntry?.SubCategory != null &&
                            x.DirectoryEntry.SubCategory.CategoryId == cat.CategoryId))
                    {
                        model.UnavailableCategories.Add(cat.CategoryId, label);
                    }
                    else
                    {
                        model.AvailableCategories.Add(cat.CategoryId, label);
                    }
                }

                model.AvailableCategories = model.AvailableCategories.OrderBy(kv => kv.Value)
                                                                      .ToDictionary(kv => kv.Key, kv => kv.Value);
                model.UnavailableCategories = model.UnavailableCategories.OrderBy(kv => kv.Value)
                                                                          .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            return this.View(model);
        }

        //[HttpGet]
        //[AllowAnonymous]
        //[Route("sponsoredlisting/selectlisting")]
        //public async Task<IActionResult> SelectListing(
        //    SponsorshipType sponsorshipType = SponsorshipType.MainSponsor,
        //    int? subCategoryId = null,
        //    int? categoryId = null)
        //{
        //    // Determine which “typeId” to use (subcategory vs. category)
        //    int? typeId = sponsorshipType == SponsorshipType.CategorySponsor
        //                  ? categoryId
        //                  : subCategoryId;

        //    // Count active sponsors of this type
        //    var totalActive = await this.sponsoredListingRepository
        //                               .GetActiveSponsorsCountAsync(sponsorshipType, typeId)
        //                               .ConfigureAwait(false);

        //    // Enforce business rules for SubcategorySponsor
        //    if (sponsorshipType == SponsorshipType.SubcategorySponsor && subCategoryId.HasValue)
        //    {
        //        var entriesInSub = await this.directoryEntryRepository
        //                                   .GetActiveEntriesByCategoryAsync(subCategoryId.Value)
        //                                   .ConfigureAwait(false);
        //        this.ViewBag.CanAdvertise =
        //            totalActive < Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings &&
        //            entriesInSub.Count() >= Common.Constants.IntegerConstants.MinimumSponsoredActiveSubcategories;
        //    }

        //    // Enforce business rules for CategorySponsor
        //    else if (sponsorshipType == SponsorshipType.CategorySponsor && categoryId.HasValue)
        //    {
        //        var entriesInCat = await this.directoryEntryRepository
        //                                   .GetActiveEntriesByCategoryAsync(categoryId.Value)
        //                                   .ConfigureAwait(false);
        //        this.ViewBag.CanAdvertise =
        //            totalActive < Common.Constants.IntegerConstants.MaxCategorySponsoredListings &&
        //            entriesInCat.Count() >= Common.Constants.IntegerConstants.MinimumSponsoredActiveCategories;
        //    }

        //    // Pass parameters to the view
        //    this.ViewBag.SponsorshipType = sponsorshipType;
        //    this.ViewBag.SubCategoryId = subCategoryId;
        //    this.ViewBag.CategoryId = categoryId;

        //    // Fetch the filtered list of entries
        //    var entries = await this.FilterEntries(sponsorshipType, typeId)
        //                           .ConfigureAwait(false);

        //    return this.View("SelectListing", entries);
        //}

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectlisting")]
        public async Task<IActionResult> SelectListing(
          SponsorshipType sponsorshipType = SponsorshipType.MainSponsor,
          int? subCategoryId = null,
          int? categoryId = null)
        {
            var totalActiveListings = await this.sponsoredListingRepository
                                                .GetActiveSponsorsCountAsync(sponsorshipType, subCategoryId);

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                if (subCategoryId != null)
                {
                    var totalActiveEntriesInCategory = await this.directoryEntryRepository
                                                                 .GetActiveEntriesByCategoryAsync(subCategoryId.Value);

                    this.ViewBag.CanAdvertise =
                            totalActiveListings < Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings &&
                            totalActiveEntriesInCategory.Count() >= Common.Constants.IntegerConstants.MinimumSponsoredActiveSubcategories;
                }
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor && categoryId.HasValue)
            {
                var entriesInCat = await this.directoryEntryRepository
                                           .GetActiveEntriesByCategoryAsync(categoryId.Value)
                                           .ConfigureAwait(false);
                this.ViewBag.CanAdvertise =
                    totalActiveListings < Common.Constants.IntegerConstants.MaxCategorySponsoredListings &&
                    entriesInCat.Count() >= Common.Constants.IntegerConstants.MinimumSponsoredActiveCategories;
            }

            this.ViewBag.SponsorshipType = sponsorshipType;

            IEnumerable<DirectoryEntry> entries = await this.FilterEntries(subCategoryId);

            return this.View("SelectListing", entries);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
            int directoryEntryId,
            SponsorshipType sponsorshipType,
            int? subCategoryId = null,
            int? categoryId = null)
        {
            if (sponsorshipType == SponsorshipType.Unknown)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSponsorshipType });
            }

            var directoryEntry = await this.directoryEntryRepository
                                           .GetByIdAsync(directoryEntryId)
                                           .ConfigureAwait(false);
            if (directoryEntry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            if (!this.IsOldEnough(directoryEntry))
            {
                return this.BadRequest(new { Error = $"Unverified listing must be listed for at least {IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising} days before advertising." });
            }

            // Determine which ID to use for counting
            int? typeId = sponsorshipType == SponsorshipType.CategorySponsor
                          ? categoryId
                          : subCategoryId;

            var currentListings = await this.sponsoredListingRepository
                                            .GetActiveSponsorsByTypeAsync(sponsorshipType)
                                            .ConfigureAwait(false);
            var isCurrentSponsor = currentListings?.Any(x => x.DirectoryEntryId == directoryEntryId) == true;

            var totalActiveListings = await this.sponsoredListingRepository
                                              .GetActiveSponsorsCountAsync(sponsorshipType, typeId)
                                              .ConfigureAwait(false);
            if (!isCurrentSponsor && !CanAdvertise(sponsorshipType, totalActiveListings))
            {
                return this.BadRequest(new { Error = StringConstants.MaximumNumberOfSponsorsReached });
            }

            // Set ViewBag values for the view
            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.Subcategory = FormattingHelper.SubcategoryFormatting(directoryEntry.SubCategory?.Category.Name, directoryEntry.SubCategory?.Name);
                this.ViewBag.SubCategoryId = directoryEntry.SubCategoryId;
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor)
            {
                var catName = directoryEntry.SubCategory?.Category.Name;
                this.ViewBag.Category = catName;
                this.ViewBag.CategoryId = categoryId;
            }

            this.ViewBag.DirectoryEntryId = directoryEntryId;
            this.ViewBag.SponsorshipType = sponsorshipType;

            var model = await this.GetListingDurations(sponsorshipType, typeId)
                                  .ConfigureAwait(false);
            return this.View(model);
        }
        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
            int directoryEntryId,
            int selectedOfferId,
            int? subCategoryId = null,
            int? categoryId = null)
        {
            var selectedOffer = await this.sponsoredListingOfferRepository
                                         .GetByIdAsync(selectedOfferId)
                                         .ConfigureAwait(false);
            if (selectedOffer == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var directoryEntry = await this.directoryEntryRepository
                                           .GetByIdAsync(directoryEntryId)
                                           .ConfigureAwait(false);
            if (directoryEntry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidListing });
            }

            // decide which ID to use in reservation group
            int? typeId = selectedOffer.SponsorshipType == SponsorshipType.CategorySponsor
                          ? categoryId
                          : subCategoryId;

            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(
                                        selectedOffer.SponsorshipType,
                                        typeId ?? 0);

            var isActiveSponsor = await this.sponsoredListingRepository
                                          .IsSponsoredListingActive(directoryEntryId, selectedOffer.SponsorshipType)
                                          .ConfigureAwait(false);

            var totalActiveListings = await this.sponsoredListingRepository
                                               .GetActiveSponsorsCountAsync(selectedOffer.SponsorshipType, typeId)
                                               .ConfigureAwait(false);
            var totalActiveReservations = await this.sponsoredListingReservationRepository
                                                  .GetActiveReservationsCountAsync(reservationGroup)
                                                  .ConfigureAwait(false);

            if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, selectedOffer.SponsorshipType)
                && !isActiveSponsor)
            {
                return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
            }

            var reservationExpirationDate = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
            var reservation = await this.sponsoredListingReservationRepository
                                        .CreateReservationAsync(reservationExpirationDate, reservationGroup)
                                        .ConfigureAwait(false);

            return this.RedirectToAction(
                "ConfirmNowPayments",
                new
                {
                    directoryEntryId,
                    selectedOfferId,
                    rsvId = reservation.ReservationGuid,
                    subCategoryId,
                    categoryId
                });
        }

        //
        // GET /sponsoredlisting/subcategoryselection
        //
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/subcategoryselection")]
        public async Task<IActionResult> SubCategorySelection(
            int subCategoryId,
            Guid? rsvId = null)
        {
            const SponsorshipType type = SponsorshipType.SubcategorySponsor;
            var typeId = subCategoryId;

            // handle reservation creation / validation just like before
            if (rsvId == null)
            {
                var totalActive = await this.sponsoredListingRepository
                                            .GetActiveSponsorsCountAsync(type, typeId)
                                            .ConfigureAwait(false);
                var group = ReservationGroupHelper.BuildReservationGroupName(type, typeId);
                var totalResv = await this.sponsoredListingReservationRepository
                                           .GetActiveReservationsCountAsync(group)
                                           .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActive, totalResv, type))
                {
                    return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
                }

                var exp = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var res = await this.sponsoredListingReservationRepository
                                    .CreateReservationAsync(exp, group)
                                    .ConfigureAwait(false);

                return this.RedirectToAction(
                    nameof(this.SubCategorySelection),
                    new { subCategoryId, rsvId = res.ReservationGuid });
            }

            // ensure reservation still valid
            var existing = await this.sponsoredListingReservationRepository
                                     .GetReservationByGuidAsync(rsvId.Value)
                                     .ConfigureAwait(false);
            if (existing == null)
            {
                return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
            }

            this.ViewBag.ReservationGuid = rsvId;
            this.ViewBag.SubCategoryId = subCategoryId;

            var entries = await this.FilterEntries(SponsorshipType.SubcategorySponsor, subCategoryId)
                                   .ConfigureAwait(false);

            return this.View("SubCategorySelection", entries);
        }

        //
        // GET /sponsoredlisting/categoryselection
        //
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/categoryselection")]
        public async Task<IActionResult> CategorySelection(
            int categoryId,
            Guid? rsvId = null)
        {
            const SponsorshipType type = SponsorshipType.CategorySponsor;
            var typeId = categoryId;

            if (rsvId == null)
            {
                var totalActive = await this.sponsoredListingRepository
                                            .GetActiveSponsorsCountAsync(type, typeId)
                                            .ConfigureAwait(false);
                var group = ReservationGroupHelper.BuildReservationGroupName(type, typeId);
                var totalResv = await this.sponsoredListingReservationRepository
                                           .GetActiveReservationsCountAsync(group)
                                           .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActive, totalResv, type))
                {
                    return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
                }

                var exp = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var res = await this.sponsoredListingReservationRepository
                                    .CreateReservationAsync(exp, group)
                                    .ConfigureAwait(false);

                return this.RedirectToAction(
                    nameof(this.CategorySelection),
                    new { categoryId, rsvId = res.ReservationGuid });
            }

            var existing = await this.sponsoredListingReservationRepository
                                     .GetReservationByGuidAsync(rsvId.Value)
                                     .ConfigureAwait(false);
            if (existing == null)
            {
                return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
            }

            this.ViewBag.ReservationGuid = rsvId;
            this.ViewBag.CategoryId = categoryId;

            var entries = await this.FilterEntries(SponsorshipType.CategorySponsor, categoryId)
                                   .ConfigureAwait(false);

            return this.View("CategorySelection", entries);
        }

        // GET: sponsoredlisting/confirmnowpayments
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmnowpayments")]
        public async Task<IActionResult> ConfirmNowPaymentsAsync(
            int directoryEntryId,
            int selectedOfferId,
            Guid? rsvId = null)
        {
            // 1) Load the offer & listing
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

            // 2) Figure out whether we're in sub-category or category land
            int? typeId = offer.SponsorshipType switch
            {
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId,
                _ => null
            };

            // 3) Reservation guard
            if (rsvId == null)
            {
                var group = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeId ?? 0);
                var totalActive = await this.sponsoredListingRepository
                                              .GetActiveSponsorsCountAsync(offer.SponsorshipType, typeId)
                                              .ConfigureAwait(false);
                var totalReservations = await this.sponsoredListingReservationRepository
                                                  .GetActiveReservationsCountAsync(group)
                                                  .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActive, totalReservations, offer.SponsorshipType))
                {
                    return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
                }
            }
            else
            {
                var existing = await this.sponsoredListingReservationRepository
                                         .GetReservationByGuidAsync(rsvId.Value)
                                         .ConfigureAwait(false);
                if (existing == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            // 4) Stuff for the view so your form can round-trip
            this.ViewBag.ReservationGuid = rsvId;
            if (offer.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.SubCategoryId = typeId;
            }
            else if (offer.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.CategoryId = typeId;
            }

            // 5) Build the confirmation view model
            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);
            var current = await this.sponsoredListingRepository
                                      .GetActiveSponsorsByTypeAsync(offer.SponsorshipType)
                                      .ConfigureAwait(false);

            var vm = GetConfirmationModel(offer, entry, link2Name, link3Name, current);

            // 6) Determine if this is an extension or a fresh purchase
            var isCurrent = current.Any(x => x.DirectoryEntryId == directoryEntryId);
            if (!isCurrent)
            {
                // brand-new slot
                vm.CanCreateSponsoredListing = true;
            }
            else
            {
                // extension: re-check capacity under the hood
                var totalActive = await this.sponsoredListingRepository
                                            .GetActiveSponsorsCountAsync(offer.SponsorshipType, typeId)
                                            .ConfigureAwait(false);
                vm.CanCreateSponsoredListing = CanAdvertise(offer.SponsorshipType, totalActive);
                if (!vm.CanCreateSponsoredListing)
                {
                    vm.NextListingExpiration = current.Min(x => x.CampaignEndDate);
                }
            }

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

            if (rsvId == null)
            {
                var isActiveSponsor = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, sponsoredListingOffer.SponsorshipType);
                var totalActiveListings = await this.sponsoredListingRepository
                                                    .GetActiveSponsorsCountAsync(
                                                        sponsoredListingOffer.SponsorshipType,
                                                        directoryEntry.SubCategoryId);
                var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(
                                                                    sponsoredListingOffer.SponsorshipType,
                                                                    directoryEntry.SubCategoryId);
                var totalActiveReservations = await this.sponsoredListingReservationRepository
                                                        .GetActiveReservationsCountAsync(reservationGroup);

                if (!CanPurchaseListing(
                    totalActiveListings, totalActiveReservations, sponsoredListingOffer.SponsorshipType) && !isActiveSponsor)
                {
                    return this.BadRequest(new { Error = string.Format(StringConstants.CheckoutInProcess, IntegerConstants.ReservationMinutes) });
                }
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }

                var existingInvoice = await this.sponsoredListingInvoiceRepository.GetByReservationGuidAsync(existingReservation.ReservationGuid);

                if (existingInvoice != null)
                {
                    return this.BadRequest(new { Error = StringConstants.InvoiceAlreadyCreated });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;
            var existingListing = await this.sponsoredListingRepository.GetActiveSponsorAsync(directoryEntryId, sponsoredListingOffer.SponsorshipType);
            var startDate = DateTime.UtcNow;

            if (existingListing != null)
            {
                startDate = existingListing.CampaignEndDate;
            }

            var invoice = await this.CreateInvoice(directoryEntry, sponsoredListingOffer, startDate, ipAddress);
            var invoiceRequest = this.GetInvoiceRequest(sponsoredListingOffer, invoice);

            this.paymentService.SetDefaultUrls(invoiceRequest);

            var invoiceFromProcessor = await this.paymentService.CreateInvoice(invoiceRequest);

            if (invoiceFromProcessor == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice." });
            }

            if (invoiceFromProcessor.Id == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice ID." });
            }

            invoice.ReservationGuid = (rsvId == null) ? Guid.Empty : rsvId.Value;
            invoice.ProcessorInvoiceId = invoiceFromProcessor.Id;
            invoice.PaymentProcessor = PaymentProcessor.NOWPayments;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(invoiceRequest);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(invoiceFromProcessor);
            invoice.Email = InputHelper.SetEmail(email);

            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            if (string.IsNullOrWhiteSpace(invoiceFromProcessor?.InvoiceUrl))
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
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8);
            var callbackPayload = await reader.ReadToEndAsync();

            this.logger.LogError(callbackPayload);

            IpnPaymentMessage? ipnMessage = null;
            try
            {
                ipnMessage = JsonConvert.DeserializeObject<IpnPaymentMessage>(callbackPayload);
            }
            catch (JsonException)
            {
                return this.BadRequest(new { Error = "Failed to parse the request body." });
            }

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
            }

            var nowPaymentsSig = this.Request
                                     .Headers[NowPayments.API.Constants.StringConstants.HeaderNameAuthCallBack]
                                     .FirstOrDefault() ?? string.Empty;

            bool isValidRequest = this.paymentService.IsIpnRequestValid(
                callbackPayload,
                nowPaymentsSig,
                out string errorMsg);

            if (!isValidRequest)
            {
                return this.BadRequest(new { Error = errorMsg });
            }

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
            }

            if (ipnMessage.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID is null." });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                                    .GetByInvoiceIdAsync(Guid.Parse(ipnMessage.OrderId));

            if (invoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.PaidAmount = ipnMessage.PayAmount;
            invoice.OutcomeAmount = ipnMessage.OutcomeAmount;

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
            }

            if (ipnMessage.PaymentStatus == null)
            {
                return this.BadRequest(new { Error = "Payment status is null." });
            }

            var processorPaymentStatus = EnumHelper.ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(ipnMessage.PaymentStatus);
            var translatedValue = ConvertToInternalStatus(processorPaymentStatus);
            invoice.PaymentStatus = translatedValue;

            if (ipnMessage.PayCurrency == null)
            {
                return this.BadRequest(new { Error = "Pay currency is null." });
            }

            var processorCurrency = EnumHelper.ParseStringToEnum<Currency>(ipnMessage.PayCurrency);
            invoice.PaidInCurrency = processorCurrency;

            await this.CreateNewSponsoredListing(invoice);

            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentssuccess")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
                "StyleCop.CSharp.NamingRules",
                "SA1313:Parameter names should begin with lower-case letter",
                Justification = "This is the param from them")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);

            if (processorInvoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            if (processorInvoice.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID not found." });
            }

            var existingInvoice = await this.sponsoredListingInvoiceRepository
                                            .GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId));

            if (existingInvoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            existingInvoice.PaymentStatus = PaymentStatus.Paid;
            existingInvoice.PaymentResponse = NP_id;

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
                Name = directoryEntry.Name
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

            this.cache.Remove(StringConstants.EntriesCacheKey);
            this.cache.Remove(StringConstants.SponsoredListingsCacheKey);

            return this.RedirectToAction("List");
        }

        [AllowAnonymous]
        [Route("sponsoredlisting/offers")]
        [HttpGet]
        public async Task<IActionResult> Offers()
        {
            // Retrieve all MainSponsor offers and include the Subcategory and Category navigation properties
            var mainSponsorshipOffers = await this.sponsoredListingOfferRepository
                .GetAllByTypeAsync(SponsorshipType.MainSponsor);

            // Map, filter, and order the main sponsorship offers
            var enabledMainSponsorshipOffers = mainSponsorshipOffers
                .Select(o => new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    PriceCurrency = o.PriceCurrency,
                    Price = o.Price,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = o.Subcategory != null
                        ? FormattingHelper.SubcategoryFormatting(o.Subcategory.Category?.Name ?? StringConstants.Default, o.Subcategory.Name)
                        : StringConstants.Default
                })
                .OrderBy(o => o.CategorySubcategory == StringConstants.Default ? 0 : 1) // Entries with no Subcategory come first
                .ThenBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

            // Retrieve all SubcategorySponsor offers and include the Subcategory and Category navigation properties
            var subCategorySponsorshipOffers = await this.sponsoredListingOfferRepository
                .GetAllByTypeAsync(SponsorshipType.SubcategorySponsor);

            // Map, filter, and order the subcategory sponsorship offers
            var enabledSubCategoryOffers = subCategorySponsorshipOffers
                .Select(o => new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    PriceCurrency = o.PriceCurrency,
                    Price = o.Price,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = o.Subcategory != null
                        ? FormattingHelper.SubcategoryFormatting(o.Subcategory.Category?.Name ?? StringConstants.Default, o.Subcategory.Name)
                        : StringConstants.Default
                })
                .OrderBy(o => o.CategorySubcategory == StringConstants.Default ? 0 : 1) // Entries with no Subcategory come first
                .ThenBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

            // Pass the data to the view using a strongly typed model
            var model = new SponsoredListingOffersViewModel
            {
                MainSponsorshipOffers = enabledMainSponsorshipOffers,
                SubCategorySponsorshipOffers = enabledSubCategoryOffers
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

            // Filter listings by type
            var mainSponsorListings = listings.Where(l => l.SponsorshipType == SponsorshipType.MainSponsor).ToList();
            var subCategorySponsorListings = listings.Where(l => l.SponsorshipType == SponsorshipType.SubcategorySponsor).ToList();

            var model = new ActiveSponsoredListingViewModel
            {
                MainSponsorItems = mainSponsorListings.Select(listing => new ActiveSponsoredListingModel
                {
                    ListingName = listing.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = listing.SponsoredListingId,
                    CampaignEndDate = listing.CampaignEndDate,
                    ListingUrl = listing.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = listing.DirectoryEntryId,
                    SponsorshipType = listing.SponsorshipType
                }).ToList(),

                SubCategorySponsorItems = subCategorySponsorListings.Select(listing => new ActiveSponsoredListingModel
                {
                    ListingName = listing.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = listing.SponsoredListingId,
                    CampaignEndDate = listing.CampaignEndDate,
                    ListingUrl = listing.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = listing.DirectoryEntryId,
                    SubcategoryName = this.SetSubcategoryNameAsync(listing.SubCategoryId),
                    SponsorshipType = listing.SponsorshipType
                }).ToList()
            };

            return this.View("activelistings", model);
        }

        [Route("sponsoredlisting/list/{page?}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> List(int page = 1)
        {
            int pageSize = IntegerConstants.DefaultPageSize;
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
                    EndDate = l.CampaignEndDate
                }).ToList()
            };

            return this.View(model);
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
                SelectedDirectoryEntry = new DirectoryEntryViewModel()
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
                    SubCategoryId = directoryEntry.SubCategoryId
                },
                Offer = new SponsoredListingOfferModel()
                {
                    Description = offer.Description,
                    Days = offer.Days,
                    SponsoredListingOfferId = offer.SponsoredListingOfferId,
                    USDPrice = offer.Price,
                    SponsorshipType = offer.SponsorshipType
                },
                IsExtension = currentListings.FirstOrDefault(x => x.DirectoryEntryId == directoryEntry.DirectoryEntryId) != null
            };
        }

        private static PaymentStatus ConvertToInternalStatus(
            NowPayments.API.Enums.PaymentStatus externalStatus)
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

        private static bool CanAdvertise(
            SponsorshipType sponsorshipType,
            int totalForTypeInGroup)
        {
            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                return totalForTypeInGroup < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
            }

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                return totalForTypeInGroup < DirectoryManager.Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings;
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

        private PaymentRequest GetInvoiceRequest(
            SponsoredListingOffer sponsoredListingOffer,
            SponsoredListingInvoice invoice)
        {
            return new PaymentRequest
            {
                IsFeePaidByUser = true,
                PriceAmount = sponsoredListingOffer.Price,
                PriceCurrency = this.paymentService.PriceCurrency,
                PayCurrency = this.paymentService.PayCurrency,
                OrderId = invoice.InvoiceId.ToString(),
                OrderDescription = sponsoredListingOffer.Description
            };
        }

        private async Task<SponsoredListingInvoice> CreateInvoice(
            DirectoryEntry directoryEntry,
            SponsoredListingOffer sponsoredListingOffer,
            DateTime startDate,
            string ipAddress)
        {
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
                    IpAddress = ipAddress
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
                    USDPrice = offer.Price
                });
            }

            return model;
        }

        private async Task CreateNewSponsoredListing(SponsoredListingInvoice invoice)
        {
            if (await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                var existingSponsoredListing = await this.sponsoredListingRepository
                                                         .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId);

                if (existingSponsoredListing == null && invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    var activeListing = await this.sponsoredListingRepository
                                                  .GetActiveSponsorAsync(invoice.DirectoryEntryId, invoice.SponsorshipType);

                    if (activeListing == null)
                    {
                        // if the invoice is paid and there is no existing sponsored listing, create one
                        var sponsoredListing = await this.sponsoredListingRepository.CreateAsync(
                            new SponsoredListing()
                            {
                                DirectoryEntryId = invoice.DirectoryEntryId,
                                CampaignStartDate = invoice.CampaignStartDate,
                                CampaignEndDate = invoice.CampaignEndDate,
                                SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                                SponsorshipType = invoice.SponsorshipType,
                                SubCategoryId = invoice.SubCategoryId,
                            });

                        invoice.SponsoredListingId = sponsoredListing.SponsoredListingId;
                        await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
                    }
                    else
                    {
                        // extend the existing listing
                        activeListing.CampaignEndDate = invoice.CampaignEndDate;

                        // set the current active listing's invoice to the new invoice
                        activeListing.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId;
                        await this.sponsoredListingRepository.UpdateAsync(activeListing);

                        // set the new invoice's sponsored listing to the current active listing
                        invoice.SponsoredListingId = activeListing.SponsoredListingId;
                        await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
                    }

                    this.ClearCachedItems();
                }
            }
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntries(int? subCategoryId)
        {
            var entries = await this.directoryEntryRepository.GetAllowableAdvertisers();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategoryId == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            await this.GetSubCateogryOptions();

            return entries;
        }

        private async Task GetSubCateogryOptions()
        {
            this.ViewBag.SubCategories = (await this.subCategoryRepository
                                                    .GetAllActiveSubCategoriesAsync())
                                                    .OrderBy(sc => sc.Category.Name)
                                                    .ThenBy(sc => sc.Name)
                                                    .ToList();
        }

        private bool IsOldEnough(DirectoryEntry directoryEntry)
        {
            if (directoryEntry.CreateDate == DateTime.MinValue)
            {
                return false; // Or throw an exception if CreateDate is required
            }

            if (directoryEntry.DirectoryStatus == DirectoryStatus.Verified)
            {
                return true;
            }

            // Check if the entry is at least 30 days old
            return (DateTime.UtcNow - directoryEntry.CreateDate).TotalDays >= IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising;
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntries(SponsorshipType sponsorshipType, int? typeId)
        {
            // Start with all allowable advertisers
            var entries = await this.directoryEntryRepository
                                     .GetAllowableAdvertisers()
                                     .ConfigureAwait(false);

            // If filtering by subcategory sponsor, restrict by SubCategoryId
            if (sponsorshipType == SponsorshipType.SubcategorySponsor && typeId.HasValue)
            {
                entries = entries
                    .Where(e => e.SubCategoryId == typeId.Value)
                    .ToList();
            }
            // If filtering by category sponsor, restrict by the CategoryId of the SubCategory navigation
            else if (sponsorshipType == SponsorshipType.CategorySponsor && typeId.HasValue)
            {
                entries = entries
                    .Where(e => e.SubCategory != null &&
                                e.SubCategory.CategoryId == typeId.Value)
                    .ToList();
            }

            // Always sort alphabetically by Name
            return entries
                .OrderBy(e => e.Name)
                .ToList();
        }

    }
}