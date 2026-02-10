using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.SponsoredListing;
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
        private static readonly PaymentStatus[] HoldExtendingStatuses =
        {
            PaymentStatus.InvoiceCreated,
            PaymentStatus.Pending,
            PaymentStatus.UnderPayment
        };

        // Add these inside SponsoredListingController (once)
        private static readonly Dictionary<PaymentStatus, int> StatusRank = new ()
        {
            { PaymentStatus.Unknown,        0 },
            { PaymentStatus.InvoiceCreated, 1 }, // "Waiting"
            { PaymentStatus.UnderPayment,   2 },
            { PaymentStatus.Pending,        3 }, // Sending/Confirming/Confirmed
            { PaymentStatus.Paid,           4 },

            // Terminal (failure/test) — treat as highest for "stop further changes"
            { PaymentStatus.Expired,        5 },
            { PaymentStatus.Failed,         5 },
            { PaymentStatus.Refunded,       5 },
            { PaymentStatus.Canceled,       5 },
            { PaymentStatus.Test,           5 },
        };


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
        private readonly IAffiliateAccountRepository affiliateRepo;
        private readonly IAffiliateCommissionRepository commissionRepo;
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
            IAffiliateAccountRepository affiliateRepo,
            IAffiliateCommissionRepository commissionRepo,
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
            this.affiliateRepo = affiliateRepo;
            this.commissionRepo = commissionRepo;
        }

        [Route("advertise")]
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

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlisting/selectlisting")]
        public async Task<IActionResult> SelectListing(
            SponsorshipType sponsorshipType,
            int directoryEntryId,
            int? subCategoryId = null,
            int? categoryId = null,
            Guid? rsvId = null)
        {
            // Basic validation
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
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

            // Build reservation scope (MainSponsor uses global 0)
            var typeIdForGroup = sponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => categoryId ?? entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => subCategoryId ?? entry.SubCategoryId,
                _ => 0,
            };

            var group = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);
            int? typeIdForCapacity = sponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            // Is this an extension?
            var isExtension = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, sponsorshipType)
                .ConfigureAwait(false);

            // Carry a posted reservation if valid (no creation here)
            var hasToken = await this.TryAttachReservationAsync(rsvId, group);

            // ---- 1) SUBCATEGORY CAP FIRST (ACTIVE listings only) ----
            if (sponsorshipType == SponsorshipType.MainSponsor && !isExtension)
            {
                var subId = entry.SubCategoryId; // the subcategory of the listing being selected
                var activeInSub = await this.GetActiveMainCountForSubcategoryAsync(subId).ConfigureAwait(false);

                if (activeInSub >= DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
                {
                    var msg = await this.BuildMainSubcategoryLimitMessageAsync(subId).ConfigureAwait(false);
                    return this.BadRequest(new { Error = msg });
                }
            }

            // ---- 2) TYPE-WIDE reservation/capacity (NO subcategory wording) ----
            if (!isExtension && !hasToken)
            {
                var totalActiveListings = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(sponsorshipType, typeIdForCapacity)
                    .ConfigureAwait(false);

                var totalActiveReservations = await this.sponsoredListingReservationRepository
                    .GetActiveReservationsCountAsync(group)
                    .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, sponsorshipType))
                {
                    // Message will read: "Another checkout for Main Sponsor ...", not tied to any subcategory.
                    var msg = await this.BuildCheckoutInProcessMessageAsync(sponsorshipType, typeIdForCapacity, group);
                    return this.BadRequest(new { Error = msg });
                }

                // Create the (type-wide) reservation now
                var expiration = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);

                var details = await this.BuildReservationDetailsAsync(
                    sponsorshipType,
                    entry,
                    subCategoryId ?? entry.SubCategoryId,
                    categoryId ?? entry.SubCategory?.CategoryId);

                var res = await this.sponsoredListingReservationRepository
                    .CreateReservationAsync(expiration, group, details)
                    .ConfigureAwait(false);

                rsvId = res.ReservationGuid;

                // IMPORTANT: Do NOT create a subcategory "shadow" reservation here.
            }

            // Continue to duration selection
            return this.RedirectToAction("SelectDuration", new { directoryEntryId, sponsorshipType, rsvId });
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
        public async Task<IActionResult> SelectDurationAsync(int directoryEntryId, SponsorshipType sponsorshipType, Guid? rsvId = null)
        {
            if (sponsorshipType == SponsorshipType.Unknown)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSponsorshipType });
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            if (!this.IsOldEnough(entry))
            {
                return this.BadRequest(new { Error = $"Unverified listing must be listed for at least {IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising} days before advertising." });
            }

            var typeIdForGroup = sponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                _ => 0,
            };
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);

            // Is extension?
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, sponsorshipType);

            // Only validate/attach existing reservation; do NOT create here
            if (rsvId.HasValue)
            {
                var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);
                if (existing != null && existing.ReservationGroup == reservationGroup && existing.ExpirationDateTime > DateTime.UtcNow)
                {
                    this.ViewBag.ReservationGuid = rsvId;
                    this.ViewBag.ReservationExpiresUtc = existing.ExpirationDateTime;
                }
            }

            // Set view context
            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.Subcategory = FormattingHelper.SubcategoryFormatting(entry.SubCategory?.Category?.Name, entry.SubCategory?.Name);
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

            // Tell the view whether it needs to show the "Start Checkout" POST form
            this.ViewBag.RequiresReservationStart = !isExtension && this.ViewBag.ReservationGuid == null;

            var model = await this.GetListingDurations(sponsorshipType, entry.SubCategoryId);
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
                // ✅ Subcategory cap for Main Sponsor (ACTIVE-only) + same-subcategory live checkout
                if (offer.SponsorshipType == SponsorshipType.MainSponsor)
                {
                    var subId = entry.SubCategoryId;

                    var activeCount = await this.GetActiveMainCountForSubcategoryAsync(subId).ConfigureAwait(false);
                    if (activeCount >= DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
                    {
                        var msg = await this.BuildMainSubcategoryLimitMessageAsync(subId).ConfigureAwait(false);
                        return this.BadRequest(new { Error = msg });
                    }

                    var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subId);
                    var subReservations = await this.sponsoredListingReservationRepository
                        .GetActiveReservationsCountAsync(subGroup)
                        .ConfigureAwait(false);

                    if (subReservations > 0)
                    {
                        var msg = await this.BuildCheckoutInProcessMessageForMainSubcategoryAsync(subId).ConfigureAwait(false);
                        return this.BadRequest(new { Error = msg });
                    }
                }

                var totalActiveListings = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(offer.SponsorshipType, typeIdForCapacity)
                    .ConfigureAwait(false);

                var totalActiveReservations = await this.sponsoredListingReservationRepository
                    .GetActiveReservationsCountAsync(reservationGroup)
                    .ConfigureAwait(false);

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations, offer.SponsorshipType))
                {
                    var msg = await this.BuildCheckoutInProcessMessageAsync(offer.SponsorshipType, typeIdForCapacity, reservationGroup);
                    return this.BadRequest(new { Error = msg });
                }

                var expiration = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var details = await this.BuildReservationDetailsAsync(
                    offer.SponsorshipType,
                    entry,
                    entry.SubCategoryId,
                    entry.SubCategory?.CategoryId);

                var newReservation = await this.sponsoredListingReservationRepository
                    .CreateReservationAsync(expiration, reservationGroup, details)
                    .ConfigureAwait(false);

                rsvId = newReservation.ReservationGuid;

                // Shadow reservation for subcategory
                if (offer.SponsorshipType == SponsorshipType.MainSponsor)
                {
                    var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, entry.SubCategoryId);
                    await this.sponsoredListingReservationRepository
                        .CreateReservationAsync(expiration, subGroup, details)
                        .ConfigureAwait(false);
                }
            }

            return this.RedirectToAction(
                "ConfirmNowPayments",
                new { directoryEntryId, selectedOfferId, rsvId });
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmnowpayments")]
        public async Task<IActionResult> ConfirmNowPaymentsAsync(
            int directoryEntryId,
            int selectedOfferId,
            Guid? rsvId = null,
            string? referralCode = null)
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

            // Build scope (NOTE: MainSponsor uses global group "0")
            var typeIdForGroup = offer.SponsorshipType switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => entry.SubCategoryId,
                _ => 0,
            };
            var group = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeIdForGroup);
            int? typeIdForCapacity = offer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;

            // Is this an extension?
            var isExtension = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType)
                .ConfigureAwait(false);

            // ---- 1) Subcategory cap FIRST (ACTIVE listings only) ----
            if (!isExtension && offer.SponsorshipType == SponsorshipType.MainSponsor)
            {
                var activeInSub = await this.GetActiveMainCountForSubcategoryAsync(entry.SubCategoryId).ConfigureAwait(false);
                if (activeInSub >= DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
                {
                    var msg = await this.BuildMainSubcategoryLimitMessageAsync(entry.SubCategoryId).ConfigureAwait(false);
                    return this.BadRequest(new { Error = msg });
                }
            }

            // ---- 2) Then global reservation/capacity for the TYPE (no subcategory wording) ----
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
                        // This produces "Another checkout for Main Sponsor..." (type-wide), not subcategory.
                        var msg = await this.BuildCheckoutInProcessMessageAsync(offer.SponsorshipType, typeIdForCapacity, group);
                        return this.BadRequest(new { Error = msg });
                    }
                }
            }

            // View context
            if (offer.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.SubCategoryId = typeIdForGroup;
            }
            else if (offer.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.CategoryId = typeIdForGroup;
            }

            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var current = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(offer.SponsorshipType)
                .ConfigureAwait(false);

            referralCode ??= this.Request.Query["ref"].ToString();
            var normalizedRef = ReferralCodeHelper.NormalizeOrNull(referralCode);
            this.ViewBag.ReferralCode = normalizedRef ?? string.Empty;

            var vm = GetConfirmationModel(offer, entry, link2Name, link3Name, current);
            vm.CanCreateSponsoredListing = true;

            return this.View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/subcategoryselection")]
        public async Task<IActionResult> SubCategorySelection(int subCategoryId, Guid? rsvId = null)
        {
            this.ViewBag.SubCategoryId = subCategoryId;

            // Carry/validate an existing reservation if provided; DO NOT create one here.
            if (rsvId.HasValue)
            {
                var group = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.SubcategorySponsor, subCategoryId);
                await this.TryAttachReservationAsync(rsvId, group).ConfigureAwait(false);

                // Even if it fails, the page is still viewable without a reservation.
            }

            var entries = await this.FilterEntries(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            return this.View("SubCategorySelection", entries);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/categoryselection")]
        public async Task<IActionResult> CategorySelection(int categoryId, Guid? rsvId = null)
        {
            this.ViewBag.CategoryId = categoryId;

            // Carry/validate an existing reservation if provided; DO NOT create one here.
            if (rsvId.HasValue)
            {
                var group = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.CategorySponsor, categoryId);
                await this.TryAttachReservationAsync(rsvId, group).ConfigureAwait(false);

                // Even if it fails, the page is still viewable without a reservation.
            }

            var entries = await this.FilterEntries(SponsorshipType.CategorySponsor, categoryId).ConfigureAwait(false);
            return this.View("CategorySelection", entries);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentscallback")]
        public async Task<IActionResult> NowPaymentsCallBackAsync()
        {
            // ---- Read raw payload (needed for HMAC) ----
            string callbackPayload;
            using (var reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                callbackPayload = await reader.ReadToEndAsync();
            }

            this.logger.LogInformation("NOWPayments IPN received: {Payload}", callbackPayload);

            // ---- Verify signature ----
            var nowPaymentsSig = this.Request
                .Headers[NowPayments.API.Constants.StringConstants.HeaderNameAuthCallBack]
                .FirstOrDefault() ?? string.Empty;

            if (!this.paymentService.IsIpnRequestValid(callbackPayload, nowPaymentsSig, out var sigError))
            {
                this.logger.LogWarning("NOWPayments IPN signature invalid: {Error}", sigError);
                return this.Ok();
            }

            // ---- Deserialize ----
            IpnPaymentMessage? ipnMessage = null;
            try
            {
                ipnMessage = JsonConvert.DeserializeObject<IpnPaymentMessage>(callbackPayload);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "NOWPayments IPN deserialize failed.");
                return this.Ok();
            }

            if (ipnMessage?.PaymentStatus == null)
            {
                this.logger.LogWarning("NOWPayments IPN missing PaymentStatus. Payload: {Payload}", callbackPayload);
                return this.Ok();
            }

            // =========================================================================================
            // Association: prefer your OrderId (GUID), fallback to processor invoice_id (string compare)
            // =========================================================================================
            SponsoredListingInvoice? invoice = null;

            // Primary key: your GUID OrderId
            Guid orderGuid;
            var hasOrderGuid = Guid.TryParse(ipnMessage.OrderId, out orderGuid);

            if (hasOrderGuid)
            {
                invoice = await this.sponsoredListingInvoiceRepository.GetByInvoiceIdAsync(orderGuid);
            }

            // Fallback by processor invoice id (NOWPayments invoice_id) -> compare as string
            // ipnMessage.InvoiceId may be long?/long; ToString() on Nullable<T> is safe (empty when null)
            var ipnProcessorId = ipnMessage.InvoiceId.ToString() ?? string.Empty;
            if (invoice == null && !string.IsNullOrWhiteSpace(ipnProcessorId))
            {
                invoice = await this.sponsoredListingInvoiceRepository.GetByProcessorInvoiceIdAsync(ipnProcessorId);

                // If we found by processor id AND the IPN carries an OrderId that doesn't match the row, ignore.
                if (invoice != null && hasOrderGuid && invoice.InvoiceId != orderGuid)
                {
                    this.logger.LogWarning(
                        "IPN association via processor invoice_id {ProcessorInvoiceId} but OrderId mismatch. IPN:{IpnOrder} DB:{DbOrder}",
                        ipnProcessorId,
                        ipnMessage.OrderId,
                        invoice.InvoiceId);
                    return this.Ok();
                }
            }

            if (invoice == null)
            {
                this.logger.LogWarning(
                    "NOWPayments IPN could not associate invoice. OrderId:{OrderId} ProcessorInvoiceId:{ProcessorInvoiceId}",
                    ipnMessage.OrderId,
                    ipnProcessorId);
                return this.Ok(); // nothing to update
            }

            // -----------------------------------------------------------------------------
            // Strict dual-key guard: after first bind, require BOTH keys to line up
            // -----------------------------------------------------------------------------
            if (hasOrderGuid && invoice.InvoiceId != orderGuid)
            {
                this.logger.LogWarning(
                    "Ignoring IPN: OrderId mismatch. IPN:{IpnOrderId} DB:{DbOrderId}",
                    ipnMessage.OrderId,
                    invoice.InvoiceId);
                return this.Ok();
            }

            if (!string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId) &&
                !string.IsNullOrWhiteSpace(ipnProcessorId) &&
                !string.Equals(invoice.ProcessorInvoiceId, ipnProcessorId, StringComparison.Ordinal))
            {
                this.logger.LogWarning(
                    "Ignoring IPN: processor invoice_id mismatch. OrderId:{OrderId} DB:{DbInvoiceId} IPN:{IpnInvoiceId}",
                    ipnMessage.OrderId,
                    invoice.ProcessorInvoiceId,
                    ipnProcessorId);
                return this.Ok();
            }

            // One-time bind for legacy rows that didn't capture the processor invoice id
            if (string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId) && !string.IsNullOrWhiteSpace(ipnProcessorId))
            {
                invoice.ProcessorInvoiceId = ipnProcessorId;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
            }

            // ========================================================================
            // Monotonic status updates + audit fields; never regress and respect terminal
            // ========================================================================

            // Already fully paid? Only try affiliate and exit.
            if (invoice.PaymentStatus == PaymentStatus.Paid)
            {
                await this.TryCreateAffiliateCommissionForInvoiceAsync(invoice);
                return this.Ok();
            }

            // Treat these as terminal -> no mutation
            if (invoice.PaymentStatus is PaymentStatus.Test
                or PaymentStatus.Expired
                or PaymentStatus.Failed
                or PaymentStatus.Refunded
                or PaymentStatus.Canceled)
            {
                return this.Ok();
            }

            // Update audit fields regardless of status change
            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.PaidAmount = ipnMessage.PayAmount;
            invoice.OutcomeAmount = ipnMessage.OutcomeAmount;

            if (!string.IsNullOrWhiteSpace(ipnMessage.PayCurrency))
            {
                invoice.PaidInCurrency = DirectoryManager.Utilities.Helpers.EnumHelper
                    .ParseStringToEnum<Currency>(ipnMessage.PayCurrency);
            }

            // Map external -> internal and enforce forward-only transition
            var processorStatus = DirectoryManager.Utilities.Helpers.EnumHelper
                .ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(ipnMessage.PaymentStatus);

            var proposed = ConvertToInternalStatus(processorStatus);
            var current = invoice.PaymentStatus;

            if (!IsTerminal(current))
            {
                var curRank = StatusRank[current];
                var nxtRank = StatusRank[proposed];

                if (nxtRank > curRank)
                {
                    invoice.PaymentStatus = proposed;
                }
                else if (nxtRank < curRank)
                {
                    this.logger.LogInformation(
                        "Ignoring status regression on invoice {OrderId}: {Current} -> {Proposed}",
                        invoice.InvoiceId,
                        current,
                        proposed);
                }

                // else equal -> duplicate; keep as-is
            }

            // Extend the reservation hold while it's active-but-not-paid
            if (HoldExtendingStatuses.Contains(invoice.PaymentStatus))
            {
                await this.EnsureHoldFromInvoiceAsync(
                    invoice,
                    minimumHold: TimeSpan.FromHours(2),
                    maxFromNow: TimeSpan.FromHours(3));
            }

            // Persist updates and (if Paid) create/extend listing (this method updates invoice first)
            await this.CreateNewSponsoredListing(invoice);

            // If now paid, attempt affiliate commission
            if (invoice.PaymentStatus == PaymentStatus.Paid)
            {
                await this.TryCreateAffiliateCommissionForInvoiceAsync(invoice);
            }

            return this.Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmnowpayments")]
        public async Task<IActionResult> ConfirmedNowPaymentsAsync(
                   int directoryEntryId,
                   int selectedOfferId,
                   Guid? rsvId = null,
                   string? email = null,
                   string? referralCode = null)
        {
            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.NotFound();
            }

            // Captcha context
            var ctx = (this.Request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ctx))
            {
                ctx = "sponsoredlisting-confirmnowpayments";
            }

            // Validate CAPTCHA
            var captchaOk = CaptchaTools.Validate(this.HttpContext, ctx, this.Request.Form["Captcha"].ToString(), consume: true);
            if (!captchaOk)
            {
                // Rebuild the same view model like your email-validation failure path does
                var sponsoredListingOfferFail = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);
                if (sponsoredListingOfferFail == null)
                {
                    return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
                }

                var directoryEntryFail = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
                if (directoryEntryFail == null)
                {
                    return this.BadRequest(new { Error = StringConstants.DirectoryEntryNotFound });
                }

                var typeIdForGroupFail = sponsoredListingOfferFail.SponsorshipType switch
                {
                    SponsorshipType.MainSponsor => 0,
                    SponsorshipType.CategorySponsor => directoryEntryFail.SubCategory?.CategoryId ?? 0,
                    SponsorshipType.SubcategorySponsor => directoryEntryFail.SubCategoryId,
                    _ => 0,
                };

                if (sponsoredListingOfferFail.SponsorshipType == SponsorshipType.SubcategorySponsor)
                {
                    this.ViewBag.SubCategoryId = typeIdForGroupFail;
                }
                else if (sponsoredListingOfferFail.SponsorshipType == SponsorshipType.CategorySponsor)
                {
                    this.ViewBag.CategoryId = typeIdForGroupFail;
                }

                var link2NameFail = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
                var link3NameFail = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
                var currentFail = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(sponsoredListingOfferFail.SponsorshipType);

                var normalizedRefFail = ReferralCodeHelper.NormalizeOrNull(referralCode);
                this.ViewBag.ReferralCode = normalizedRefFail ?? string.Empty;

                await this.TryAttachReservationAsync(rsvId, ReservationGroupHelper.BuildReservationGroupName(
                    sponsoredListingOfferFail.SponsorshipType, typeIdForGroupFail));

                var vmFail = GetConfirmationModel(sponsoredListingOfferFail, directoryEntryFail, link2NameFail, link3NameFail, currentFail);
                vmFail.CanCreateSponsoredListing = true;

                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
                this.ViewBag.PrefillEmail = email;

                return this.View("ConfirmNowPayments", vmFail);
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

            // Validate/attach reservation or capacity (existing logic)
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
                    var msg = await this.BuildCheckoutInProcessMessageAsync(sponsoredListingOffer.SponsorshipType, typeIdForCapacity, group);
                    return this.BadRequest(new { Error = msg });
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
                    if (existingInvoice.PaymentStatus == PaymentStatus.Paid)
                    {
                        return this.BadRequest(new { Error = StringConstants.InvoiceAlreadyCreated });
                    }

                    existingInvoice.PaymentStatus = PaymentStatus.Canceled;
                    existingInvoice.ReservationGuid = Guid.Empty;
                    await this.sponsoredListingInvoiceRepository.UpdateAsync(existingInvoice);
                }
            }

            // Email validation (unchanged)
            string? normalizedEmail = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var (okEmail, norm, emailError) = EmailValidationHelper.Validate(email);
                if (!okEmail)
                {
                    if (sponsoredListingOffer.SponsorshipType == SponsorshipType.SubcategorySponsor)
                    {
                        this.ViewBag.SubCategoryId = typeIdForGroup;
                    }
                    else if (sponsoredListingOffer.SponsorshipType == SponsorshipType.CategorySponsor)
                    {
                        this.ViewBag.CategoryId = typeIdForGroup;
                    }

                    var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
                    var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
                    var current = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(sponsoredListingOffer.SponsorshipType);

                    var normalizedRef = ReferralCodeHelper.NormalizeOrNull(referralCode);
                    this.ViewBag.ReferralCode = normalizedRef ?? string.Empty;

                    await this.TryAttachReservationAsync(rsvId, group);

                    var vm = GetConfirmationModel(sponsoredListingOffer, directoryEntry, link2Name, link3Name, current);
                    vm.CanCreateSponsoredListing = true;

                    this.ModelState.AddModelError("Email", emailError!);
                    this.ViewBag.PrefillEmail = email;
                    return this.View("ConfirmNowPayments", vm);
                }

                normalizedEmail = norm!;
            }

            // ✅ Subcategory cap for Main Sponsor → limit message (only for NEW purchases)
            var isActiveForScope = await this.sponsoredListingRepository
                .IsSponsoredListingActive(directoryEntryId, sponsoredListingOffer.SponsorshipType)
                .ConfigureAwait(false);

            if (!isActiveForScope && sponsoredListingOffer.SponsorshipType == SponsorshipType.MainSponsor)
            {
                var subId = directoryEntry.SubCategoryId;
                if (!await this.CanPurchaseMainWithinSubcategoryAsync(subId).ConfigureAwait(false))
                {
                    var msg = await this.BuildMainSubcategoryLimitMessageAsync(subId).ConfigureAwait(false);
                    return this.BadRequest(new { Error = msg });
                }
            }

            // Proceed: create invoice (unchanged)
            this.ViewBag.ReservationGuid = rsvId;

            var existingListing = await this.sponsoredListingRepository
                .GetActiveSponsorAsync(directoryEntryId, sponsoredListingOffer.SponsorshipType);

            var startDate = existingListing?.CampaignEndDate ?? DateTime.UtcNow;

            var invoice = await this.CreateInvoice(directoryEntry, sponsoredListingOffer, startDate, ipAddress);

            if (DirectoryManager.Utilities.Helpers.ReferralCodeHelper
                    .TryNormalize(referralCode, out var normalizedRefCode, out _)
                && !string.IsNullOrEmpty(normalizedRefCode))
            {
                invoice.ReferralCodeUsed = normalizedRefCode;
            }

            var invoiceRequest = this.GetInvoiceRequest(sponsoredListingOffer, invoice);
            this.paymentService.SetDefaultUrls(invoiceRequest);

            var invoiceFromProcessor = await this.paymentService.CreateInvoice(invoiceRequest);
            if (invoiceFromProcessor == null || invoiceFromProcessor.Id == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice." });
            }

            SetInvoiceProperties(rsvId, normalizedEmail, invoice, invoiceRequest, invoiceFromProcessor);
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            if (string.IsNullOrWhiteSpace(invoiceFromProcessor.InvoiceUrl))
            {
                return this.BadRequest(new { Error = "Failed to get invoice URL." });
            }

            return this.Redirect(invoiceFromProcessor.InvoiceUrl);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentssuccess")]
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
                existingInvoice.PaymentResponse = NP_id; // audit
                await this.sponsoredListingInvoiceRepository.UpdateAsync(existingInvoice);

                var vmCanceled = new SuccessViewModel
                {
                    OrderId = existingInvoice.InvoiceId,
                    ListingEndDate = existingInvoice.CampaignEndDate
                };
                return this.View("NowPaymentsSuccess", vmCanceled);
            }

            // Mark paid if not already
            if (existingInvoice.PaymentStatus != PaymentStatus.Paid)
            {
                existingInvoice.PaymentStatus = PaymentStatus.Paid;
                existingInvoice.PaymentResponse = NP_id;
            }

            // Create/extend the listing (already idempotent and persists invoice)
            await this.CreateNewSponsoredListing(existingInvoice);

            // attempt to create the affiliate commission on success as well
            await this.TryCreateAffiliateCommissionForInvoiceAsync(existingInvoice);

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
                SponsoredListingInvoiceId = listing.SponsoredListingInvoiceId
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

        private async Task<string> BuildReservationDetailsAsync(
            SponsorshipType sponsorshipType,
            DirectoryEntry entry,
            int? subCategoryId,
            int? categoryId)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"Type={sponsorshipType}; ListingId={entry.DirectoryEntryId}; ListingName=\"{entry.Name}\"");

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                // subId is INT (non-null). If neither value is present, fall back to 0.
                int subId = subCategoryId ?? entry.SubCategoryId;
                string catName = "Unknown Category";
                string subName = "Unknown Subcategory";

                if (subId > 0)
                {
                    var sub = await this.subCategoryRepository.GetByIdAsync(subId).ConfigureAwait(false);
                    if (sub != null)
                    {
                        subName = sub.Name;
                        var cat = sub.Category ?? await this.categoryRepository.GetByIdAsync(sub.CategoryId).ConfigureAwait(false);
                        if (cat != null)
                        {
                            catName = cat.Name;
                        }
                    }
                }

                sb.Append($"; SubcategoryId={subId}; Scope=\"{catName} > {subName}\"");
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor)
            {
                // catId also ends up non-null (0 fallback if unknown)
                int catId = categoryId ?? entry.SubCategory?.CategoryId ?? 0;
                string catName = "Unknown Category";

                if (catId > 0)
                {
                    var cat = await this.categoryRepository.GetByIdAsync(catId).ConfigureAwait(false);
                    if (cat != null)
                    {
                        catName = cat.Name;
                    }
                }

                sb.Append($"; CategoryId={catId}; Category=\"{catName}\"");
            }

            return sb.ToString();
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

        private static bool IsTerminal(PaymentStatus s) =>
            s is PaymentStatus.Paid or PaymentStatus.Expired or PaymentStatus.Failed
              or PaymentStatus.Refunded or PaymentStatus.Canceled or PaymentStatus.Test;

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

        /// <summary>
        /// If the invoice has a reservation, extend that reservation so that at least
        /// <paramref name="minimumHold"/> remains, but never beyond <paramref name="maxFromNow"/>.
        /// </summary>
        private async Task EnsureHoldFromInvoiceAsync(
            SponsoredListingInvoice invoice,
            TimeSpan minimumHold,
            TimeSpan maxFromNow)
        {
            if (invoice == null ||
                invoice.ReservationGuid == Guid.Empty ||
                !HoldExtendingStatuses.Contains(invoice.PaymentStatus))
            {
                return;
            }

            var target = DateTime.UtcNow.Add(minimumHold);
            var hardCap = DateTime.UtcNow.Add(maxFromNow);
            if (target > hardCap)
            {
                target = hardCap;
            }

            await this.sponsoredListingReservationRepository
                .ExtendExpirationAsync(invoice.ReservationGuid, target)
                .ConfigureAwait(false);
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
            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false))
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
                    }).ConfigureAwait(false);

                invoice.SponsoredListingId = created.SponsoredListingId;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false);
                this.ClearCachedItems();
                return;
            }

            // Active listing exists → EXTEND end date if later, but ALWAYS link latest invoice
            var proposedEnd = invoice.CampaignEndDate;
            var changed = false;

            if (proposedEnd > activeListing.CampaignEndDate)
            {
                activeListing.CampaignEndDate = proposedEnd;
                changed = true;
            }

            // Always ensure the listing points to the newest paid invoice
            if (activeListing.SponsoredListingInvoiceId != invoice.SponsoredListingInvoiceId)
            {
                activeListing.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId;
                changed = true;
            }

            if (changed)
            {
                await this.sponsoredListingRepository.UpdateAsync(activeListing).ConfigureAwait(false);
            }

            // Always link the paid invoice to the listing for bookkeeping
            invoice.SponsoredListingId = activeListing.SponsoredListingId;
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false);

            this.ClearCachedItems();
        }

        /// <summary>
        /// Build a CLEAR, capacity-based message for Main Sponsor subcategory cap.
        /// Never mentions reservations; always frames as "limit reached".
        /// </summary>
        private async Task<string> BuildMainSubcategoryLimitMessageAsync(int subCategoryId)
        {
            // Reuse existing labeler to print: subcategory "Category > Subcategory"
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);

            // Optional: show a rough next opening based on active listings in this subcategory
            var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
            var nextText = next.HasValue
                ? $" Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC."
                : string.Empty;

            return $"No ad space available right now for Main Sponsor in {scope}. " +
                   $"This subcategory is limited to {DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory} Main Sponsors and it is full." +
                   nextText;
        }

        private async Task<(int active, int reservations)> GetMainSubcategoryCountsAsync(int subCategoryId)
        {
            var allActiveMain = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)
                .ConfigureAwait(false);

            var active = allActiveMain.Count(x => x.SubCategoryId == subCategoryId);

            var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subCategoryId);
            var reservations = await this.sponsoredListingReservationRepository
                .GetActiveReservationsCountAsync(subGroup)
                .ConfigureAwait(false);

            return (active, reservations);
        }

        private async Task<bool> CanPurchaseMainWithinSubcategoryAsync(int subCategoryId)
        {
            var active = await this.GetActiveMainCountForSubcategoryAsync(subCategoryId).ConfigureAwait(false);
            return active < DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory; // allow until we hit 2 active
        }

        private async Task<DateTime?> GetNextOpeningUtcForMainSubcategoryAsync(int subCategoryId)
        {
            var utcNow = DateTime.UtcNow;

            var all = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)
                .ConfigureAwait(false);

            var next = all
                .Where(x => x.SubCategoryId == subCategoryId &&
                            x.CampaignEndDate > utcNow) // only future openings
                .OrderBy(x => x.CampaignEndDate) // soonest first
                .Select(x => (DateTime?)x.CampaignEndDate)
                .FirstOrDefault();                       // null if none

            return next;
        }

        private async Task<string> BuildCheckoutInProcessMessageForMainSubcategoryAsync(int subCategoryId)
        {
            // Re-use existing labeler to get: subcategory "Category > Subcategory"
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);

            var (active, reservations) = await this.GetMainSubcategoryCountsAsync(subCategoryId).ConfigureAwait(false);

            if (active >= DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
            {
                var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
                if (next.HasValue)
                {
                    return $"No ad space available right now for Main Sponsor in {scope}. Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC.";
                }

                return $"No ad space available right now for Main Sponsor in {scope}.";
            }

            var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subCategoryId);
            var expiration = await this.sponsoredListingReservationRepository
                .GetActiveReservationExpirationAsync(subGroup)
                .ConfigureAwait(false);

            if (expiration.HasValue)
            {
                var expiryUtc = expiration.Value;
                var minutesLeft = Math.Max(1, (int)Math.Ceiling((expiryUtc - DateTime.UtcNow).TotalMinutes));
                return $"Another checkout for Main Sponsor in {scope} is in process and will expire at {expiryUtc:yyyy-MM-dd HH:mm} UTC (in {minutesLeft} minutes).";
            }

            return $"Another checkout is currently in process for Main Sponsor in {scope}.";
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
                        if (cat != null)
                        {
                            return $"category \"{cat.Name}\"";
                        }
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

        private async Task<int> GetActiveMainCountForSubcategoryAsync(int subCategoryId)
        {
            var allActiveMain = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)
                .ConfigureAwait(false);

            var count = 0;
            foreach (var s in allActiveMain)
            {
                int subId = 0;
                if (s.SubCategoryId.HasValue && s.SubCategoryId.Value > 0)
                {
                    subId = s.SubCategoryId.Value;
                }
                else if (s.DirectoryEntry != null)
                {
                    // fallback to the directory entry’s current subcategory
                    subId = s.DirectoryEntry.SubCategoryId;
                }

                if (subId == subCategoryId)
                {
                    count++;
                }
            }

            return count;
        }

        private async Task TryCreateAffiliateCommissionForInvoiceAsync(SponsoredListingInvoice invoice, CancellationToken ct = default)
        {
            if (invoice == null || invoice.PaymentStatus != PaymentStatus.Paid)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(invoice.ReferralCodeUsed))
            {
                return;
            }

            if (!ReferralCodeHelper.TryNormalize(invoice.ReferralCodeUsed, out var code, out _))
            {
                return;
            }

            var affiliate = await this.affiliateRepo.GetByReferralCodeAsync(code!, ct);
            if (affiliate == null)
            {
                return;
            }

            if (await this.commissionRepo.ExistsForInvoiceAsync(invoice.SponsoredListingInvoiceId, ct))
            {
                return;
            }

            // ✅ Only consider other paid invoices (exclude this one)
            var hasOtherPaid = await this.sponsoredListingInvoiceRepository
                .HasAnyPaidInvoiceForDirectoryEntryAsync(invoice.DirectoryEntryId, invoice.SponsoredListingInvoiceId, ct);
            if (hasOtherPaid)
            {
                return; // not first paid → no commission
            }

            var amountDue = Math.Round(invoice.OutcomeAmount * 0.50m, 8);
            var commission = new AffiliateCommission
            {
                SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                AffiliateAccountId = affiliate.AffiliateAccountId,
                AmountDue = amountDue,
                PayoutCurrency = invoice.PaidInCurrency,
                PayoutStatus = CommissionPayoutStatus.Pending
            };

            await this.commissionRepo.AddAsync(commission, ct);
        }
    }
}