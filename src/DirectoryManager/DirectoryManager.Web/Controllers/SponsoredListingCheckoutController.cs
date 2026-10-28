using System.Text;
using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
using DirectoryManager.Common.Helpers;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.SponsoredListing;
using DirectoryManager.Web.Models.Sponsorship;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingCheckoutController : BaseController
    {
        private const string ConfirmCheckoutView = "ConfirmCheckout";
        private const string CaptchaContextCheckout = "sponsoredlisting-confirmcheckout";
        private const string CaptchaContextSelectDuration = "sponsoredlisting-selectduration";

        // Set in the session when the buyer solves the select-duration captcha, so the confirm step
        // doesn't ask again. One invoice per solve; it also lapses after CheckoutVerifiedLifetime.
        private const string CheckoutVerifiedSessionKeyPrefix = "sponsoredlisting-verified:";
        private static readonly TimeSpan CheckoutVerifiedLifetime = TimeSpan.FromMinutes(30);

        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly INowPaymentsService paymentService;
        private readonly IBtcPayServerService btcPayServerService;
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISponsoredListingReservationRepository sponsoredListingReservationRepository;
        private readonly ICacheService cacheService;
        private readonly ISponsoredListingActivator activator;
        private readonly ILogger<SponsoredListingCheckoutController> logger;
        private readonly IDirectoryEntryAdditionalSubcategoryRepository? additionalSubcategoryRepo;

        public SponsoredListingCheckoutController(
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            INowPaymentsService paymentService,
            IBtcPayServerService btcPayServerService,
            IMemoryCache cache,
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            ICacheService cacheService,
            ISponsoredListingActivator activator,
            ILogger<SponsoredListingCheckoutController> logger,
            IDirectoryEntryAdditionalSubcategoryRepository? additionalSubcategoryRepo = null)
            : base(cache)
        {
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.paymentService = paymentService;
            this.btcPayServerService = btcPayServerService;
            this.sponsoredListingOfferRepository = sponsoredListingOfferRepository;
            this.sponsoredListingReservationRepository = sponsoredListingReservationRepository;
            this.cacheService = cacheService;
            this.activator = activator;
            this.logger = logger;
            this.additionalSubcategoryRepo = additionalSubcategoryRepo;
        }

        // =====================================================================
        // SELECT LISTING
        // =====================================================================
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
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            // A subcategory sponsorship can be for one of the listing's ADDITIONAL subcategories.
            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                entry = await this.ScopeEntryAsync(entry, subCategoryId).ConfigureAwait(false);
            }

            var scopeSubCategoryId = ScopeOf(entry);

            // Sponsorship can be disabled per subcategory (kill-switch for problem categories).
            var subcatBlocked = this.BlockedBySubcategorySponsorship(entry);
            if (subcatBlocked != null)
            {
                return subcatBlocked;
            }

            // Grandfather clause: a CURRENTLY ACTIVE sponsor can extend / re-sponsor even if the
            // listing no longer meets the standard requirements. A lapsed or refunded past buyer
            // is NOT grandfathered.
            if (!this.IsEligibleToSponsor(entry)
                && !await this.sponsoredListingRepository.HasActiveSponsorshipAsync(entry.DirectoryEntryId))
            {
                return this.BadRequest(new { Error = $"To sponsor, a listing must be Verified and listed in the directory for at least {DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring} days." });
            }

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(sponsorshipType, entry, subCategoryId, categoryId);
            var group = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);
            int? typeIdForCap = sponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActiveInScopeAsync(directoryEntryId, sponsorshipType, typeIdForGroup).ConfigureAwait(false);
            var hasToken = await this.TryAttachReservationAsync(rsvId, group, directoryEntryId);

            if (sponsorshipType == SponsorshipType.MainSponsor && !isExtension)
            {
                var capError = await this.CheckMainSubcategoryCapAsync(entry.SubCategoryId);
                if (capError != null)
                {
                    return this.BadRequest(new { Error = capError });
                }
            }

            if (!isExtension && !hasToken)
            {
                // Gate entry to the funnel if the slot is already full/held — but do NOT reserve here.
                // The reservation begins when the buyer picks a duration and presses "Select" (the
                // SelectDuration POST). From there the guid flows through the URL to the confirm page,
                // and their own hold is recoverable by entry, so back-navigation reuses it.
                var capacityError = await this.CheckListingCapacityAsync(sponsorshipType, typeIdForCap, group);
                if (capacityError != null)
                {
                    return this.RedirectToCheckoutWaiting(directoryEntryId, sponsorshipType, null, scopeSubCategoryId);
                }
            }

            return this.RedirectToAction("SelectDuration", new { directoryEntryId, sponsorshipType, rsvId, scopeSubCategoryId });
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

            if (sponsorshipType == SponsorshipType.SubcategorySponsor && subCategoryId != null)
            {
                var entries = await this.directoryEntryRepository.GetActiveEntriesBySubcategoryAsync(subCategoryId.Value);
                this.ViewBag.CanAdvertise =
                    totalActiveListings < Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings &&
                    entries.Count() >= Common.Constants.IntegerConstants.MinRequiredSubcategories;
            }
            else if (sponsorshipType == SponsorshipType.CategorySponsor && categoryId.HasValue)
            {
                var entries = await this.directoryEntryRepository.GetActiveEntriesByCategoryAsync(categoryId.Value).ConfigureAwait(false);
                this.ViewBag.CanAdvertise =
                    totalActiveListings < Common.Constants.IntegerConstants.MaxCategorySponsoredListings &&
                    entries.Count() >= Common.Constants.IntegerConstants.MinRequiredCategories;
            }

            this.ViewBag.SponsorshipType = sponsorshipType;
            return this.View(await this.FilterEntriesForSelectionAsync(subCategoryId, categoryId));
        }

        // =====================================================================
        // SUBCATEGORY / CATEGORY SELECTION
        // =====================================================================
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/subcategoryselection")]
        public async Task<IActionResult> SubCategorySelection(int subCategoryId, Guid? rsvId = null)
        {
            this.ViewBag.SubCategoryId = subCategoryId;
            if (rsvId.HasValue)
            {
                await this.TryAttachReservationAsync(rsvId, ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.SubcategorySponsor, subCategoryId)).ConfigureAwait(false);
            }

            return this.View(await this.FilterEntriesByScopeAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false));
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/categoryselection")]
        public async Task<IActionResult> CategorySelection(int categoryId, Guid? rsvId = null)
        {
            this.ViewBag.CategoryId = categoryId;
            if (rsvId.HasValue)
            {
                await this.TryAttachReservationAsync(rsvId, ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.CategorySponsor, categoryId)).ConfigureAwait(false);
            }

            return this.View(await this.FilterEntriesByScopeAsync(SponsorshipType.CategorySponsor, categoryId).ConfigureAwait(false));
        }

        // =====================================================================
        // SELECT DURATION
        // =====================================================================
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(int directoryEntryId, SponsorshipType sponsorshipType, Guid? rsvId = null, int? scopeSubCategoryId = null)
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

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                entry = await this.ScopeEntryAsync(entry, scopeSubCategoryId).ConfigureAwait(false);
            }

            this.ViewBag.ScopeSubCategoryId = ScopeOf(entry);

            // Sponsorship can be disabled per subcategory (kill-switch for problem categories).
            var subcatBlocked = this.BlockedBySubcategorySponsorship(entry);
            if (subcatBlocked != null)
            {
                return subcatBlocked;
            }

            // Grandfather clause: a CURRENTLY ACTIVE sponsor can extend / re-sponsor even if the
            // listing no longer meets the standard requirements. A lapsed or refunded past buyer
            // is NOT grandfathered.
            if (!this.IsEligibleToSponsor(entry)
                && !await this.sponsoredListingRepository.HasActiveSponsorshipAsync(entry.DirectoryEntryId))
            {
                return this.BadRequest(new { Error = $"To sponsor, a listing must be Verified and listed in the directory for at least {DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring} days." });
            }

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(sponsorshipType, entry, null, null);
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActiveInScopeAsync(directoryEntryId, sponsorshipType, typeIdForGroup);

            // Show the countdown banner only when this buyer actually holds an active reservation for this
            // slot — from the URL guid, or (on back-navigation without a guid) recovered from the DB by
            // entry. First-time visitors have no hold yet, so no banner.
            var heldRsvId = rsvId ?? await this.sponsoredListingReservationRepository
                .GetActiveReservationGuidForEntryAsync(directoryEntryId, reservationGroup).ConfigureAwait(false);
            if (heldRsvId.HasValue)
            {
                var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(heldRsvId.Value);
                if (existing != null && existing.ReservationGroup == reservationGroup && existing.ExpirationDateTime > DateTime.UtcNow)
                {
                    this.ViewBag.ReservationGuid = heldRsvId.Value;
                    this.ViewBag.ReservationExpiresUtc = existing.ExpirationDateTime;
                }
            }

            this.SetSponsorshipScopeViewBag(sponsorshipType, entry, typeIdForGroup);
            this.ViewBag.DirectoryEntrName = entry.Name;
            this.ViewBag.DirectoryEntryId = entry.DirectoryEntryId;
            this.ViewBag.SponsorshipType = sponsorshipType;

            // The placement being bought (main slot, this category page or this subcategory page)
            // sets the price and the "this placement" line on the pricing chart.
            int? placementId = typeIdForGroup > 0 ? typeIdForGroup : null;
            this.ViewBag.PlacementId = placementId;
            this.ViewBag.PlacementLabel = sponsorshipType switch
            {
                SponsorshipType.CategorySponsor => entry.SubCategory?.Category?.Name,
                SponsorshipType.SubcategorySponsor => FormattingHelper.SubcategoryFormatting(
                    entry.SubCategory?.Category?.Name, entry.SubCategory?.Name),
                _ => null,
            };

            // Whether the chart will draw the dotted "this placement" line (only when this exact
            // category / subcategory page has its own paid history). Drives the caption copy.
            this.ViewBag.HasPlacementPricingHistory = placementId.HasValue
                && await this.sponsoredListingInvoiceRepository
                    .HasPaidPurchasesForPlacementAsync(sponsorshipType, placementId.Value)
                    .ConfigureAwait(false);
            this.ViewBag.RequiresReservationStart = !isExtension && this.ViewBag.ReservationGuid == null;

            // Renewal: new time is added after the current slot ends, so say when that is.
            if (isExtension)
            {
                var current = await this.sponsoredListingRepository
                    .GetActiveSponsorInScopeAsync(directoryEntryId, sponsorshipType, typeIdForGroup).ConfigureAwait(false);
                this.ViewBag.CurrentSponsorshipEndsUtc = current?.CampaignEndDate;
            }

            // "Recently paid" range for the price summary: purchases in the last 180 days for this exact
            // placement, or for the placement type when this page has none.
            var since = DateTime.UtcNow.Date.AddDays(-180);
            var paidInvoices = (await this.sponsoredListingInvoiceRepository.GetAllAsync().ConfigureAwait(false))
                .Where(i => SponsoredListingCheckoutHelper.IsPaidOrOverpaid(i.PaymentStatus))
                .ToList();
            var recent = PricingTrendCalculator.Purchases(
                paidInvoices.Where(i => placementId == null
                    || (sponsorshipType == SponsorshipType.CategorySponsor ? i.CategoryId == placementId : i.SubCategoryId == placementId)),
                Currency.USD, since, DateTime.UtcNow.Date, sponsorshipType);
            this.ViewBag.RecentPaidIsPlacement = recent.Count > 0 && placementId != null;
            if (recent.Count == 0)
            {
                recent = PricingTrendCalculator.Purchases(paidInvoices, Currency.USD, since, DateTime.UtcNow.Date, sponsorshipType);
            }

            if (recent.Count > 0)
            {
                this.ViewBag.RecentPaidMinPerDay = recent.Min(p => p.PerDay);
                this.ViewBag.RecentPaidMaxPerDay = recent.Max(p => p.PerDay);
                this.ViewBag.RecentPaidCount = recent.Count;
            }

            // Cross-tier existing-sponsor perk banner + struck-through prices when this listing already
            // holds a sponsorship of a different tier.
            var (perkPercent, perkBasisTier) = await this.GetCrossTierDiscountAsync(entry.DirectoryEntryId, sponsorshipType).ConfigureAwait(false);
            this.ViewBag.ExistingSponsorPerkPercent = perkPercent;
            this.ViewBag.ExistingSponsorPerkBasisTier = perkBasisTier;

            return this.View(await this.GetListingDurationsAsync(sponsorshipType, placementId, perkPercent));
        }

        // Small "why this price" chart for the checkout page: the average per-day price advertisers
        // paid for this placement TYPE over the last 6 months, plus a dotted line for this exact
        // placement (one category page or one subcategory page) and today's price for it.
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/pricing-trends-chart")]
        public async Task<IActionResult> PricingTrendsChartAsync(
            SponsorshipType sponsorshipType,
            int? placementId = null)
        {
            if (sponsorshipType == SponsorshipType.Unknown)
            {
                return this.NotFound();
            }

            if (sponsorshipType == SponsorshipType.MainSponsor || placementId <= 0)
            {
                placementId = null;
            }

            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-180);

            var invoices = await this.sponsoredListingInvoiceRepository.GetAllAsync().ConfigureAwait(false);
            var paid = invoices.Where(i => SponsoredListingCheckoutHelper.IsPaidOrOverpaid(i.PaymentStatus)).ToList();

            // Dotted line: what was paid for THIS placement, using the placement saved on each invoice
            // (so a listing that later changed subcategory still counts where it actually advertised).
            List<SponsoredListingInvoice>? highlight = null;
            string? highlightLabel = null;

            if (placementId.HasValue)
            {
                var id = placementId.Value;
                highlight = paid
                    .Where(i => i.SponsorshipType == sponsorshipType
                                && (sponsorshipType == SponsorshipType.CategorySponsor
                                    ? i.CategoryId == id
                                    : i.SubCategoryId == id))
                    .ToList();

                string? name = sponsorshipType == SponsorshipType.CategorySponsor
                    ? (await this.categoryRepository.GetByIdAsync(id).ConfigureAwait(false))?.Name
                    : (await this.subCategoryRepository.GetByIdAsync(id).ConfigureAwait(false))?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    highlightLabel = name.Length > 18 ? name.Substring(0, 17).TrimEnd() + "…" : name;
                }
            }

            // Today's price per day for THIS placement (its own price, or the standard price when it
            // has none) — the same price list the duration options on the page come from.
            var currentDaily = new Dictionary<SponsorshipType, decimal>();
            var priceOffers = await this.sponsoredListingOfferRepository
                .GetOffersForPlacementAsync(sponsorshipType, placementId)
                .ConfigureAwait(false);
            var baseOffer = priceOffers
                .OrderBy(o => o.Days)
                .FirstOrDefault(o => o.Days >= 30)
                ?? priceOffers.OrderBy(o => o.Days).FirstOrDefault();
            if (baseOffer != null && baseOffer.Days > 0)
            {
                currentDaily[sponsorshipType] = Math.Round(baseOffer.Price / baseOffer.Days, 2);
            }

            var bytes = new InvoicePlotting().CreateDailyAvgPriceByTypeChart(
                paid, Currency.USD, start, end, sponsorshipType, filterLabel: null, width: 820, height: 340,
                highlightInvoices: highlight, highlightLabel: highlightLabel,
                currentMarketDailyByType: currentDaily,
                darkTheme: true,
                showPurchases: true);

            if (bytes == null || bytes.Length == 0)
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='90'>
  <rect width='100%' height='100%' fill='#05070a'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='15' fill='#d0d6dc'>Pricing history coming soon</text>
</svg>";
                return this.File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            return this.File(bytes, StringConstants.PngImage);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(int directoryEntryId, int selectedOfferId, Guid? rsvId = null, int? scopeSubCategoryId = null)
        {
            var (entry, offer) = await this.LoadEntryAndOfferAsync(directoryEntryId, selectedOfferId, scopeSubCategoryId).ConfigureAwait(false);
            if (offer is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidListing });
            }

            scopeSubCategoryId = ScopeOf(entry);
            var scopeParam = scopeSubCategoryId.HasValue ? $"&scopeSubCategoryId={scopeSubCategoryId.Value}" : string.Empty;

            // Kill-switch + eligibility are re-checked on every POST (the GET pages aren't a gate:
            // a crafted POST could otherwise hold a slot for an ineligible listing).
            var notAllowed = await this.CannotSponsorAsync(entry).ConfigureAwait(false);
            if (notAllowed != null)
            {
                return notAllowed;
            }

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, entry, null, null);
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeIdForGroup);
            int? typeIdForCap = offer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActiveInScopeAsync(directoryEntryId, offer.SponsorshipType, typeIdForGroup).ConfigureAwait(false);

            rsvId = await this.ValidateExistingReservationAsync(rsvId, reservationGroup, directoryEntryId);

            // A captcha is required before a slot is held, so holds cannot be created in bulk
            // to block other buyers. A buyer coming back with their own live hold is not asked again.
            if (!rsvId.HasValue
                && !CaptchaTools.Validate(this.HttpContext, CaptchaContextSelectDuration, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                return this.Redirect(
                    $"/sponsoredlisting/selectduration?directoryEntryId={directoryEntryId}&sponsorshipType={offer.SponsorshipType}&captcha=0{scopeParam}");
            }

            if (!rsvId.HasValue)
            {
                this.MarkCheckoutVerified(directoryEntryId);
            }

            if (!isExtension && !rsvId.HasValue)
            {
                if (offer.SponsorshipType == SponsorshipType.MainSponsor)
                {
                    var capError = await this.CheckMainSubcategoryCapAsync(entry.SubCategoryId);
                    if (capError != null)
                    {
                        return this.BadRequest(new { Error = capError });
                    }

                    var rsvError = await this.CheckMainSubcategoryReservationAsync(entry.SubCategoryId);
                    if (rsvError != null)
                    {
                        return this.RedirectToCheckoutWaiting(directoryEntryId, offer.SponsorshipType, selectedOfferId, scopeSubCategoryId);
                    }
                }

                var capacityError = await this.CheckListingCapacityAsync(offer.SponsorshipType, typeIdForCap, reservationGroup);
                if (capacityError != null)
                {
                    return this.RedirectToCheckoutWaiting(directoryEntryId, offer.SponsorshipType, selectedOfferId, scopeSubCategoryId);
                }

                rsvId = await this.CreateReservationGuidAsync(offer.SponsorshipType, entry, entry.SubCategoryId, entry.SubCategory?.CategoryId);

                if (offer.SponsorshipType == SponsorshipType.MainSponsor)
                {
                    await this.CreateShadowSubcategoryReservationAsync(entry.SubCategoryId, entry, rsvId!.Value);
                }
            }

            return this.RedirectToAction("ConfirmCheckout", new { directoryEntryId, selectedOfferId, rsvId, scopeSubCategoryId });
        }

        // =====================================================================
        // CONFIRM CHECKOUT
        // =====================================================================
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmcheckout")]
        public async Task<IActionResult> ConfirmCheckoutAsync(
            int directoryEntryId,
            int selectedOfferId,
            Guid? rsvId = null,
            int? scopeSubCategoryId = null)
        {
            var v = await this.ValidateConfirmRequestAsync(directoryEntryId, selectedOfferId, rsvId, scopeSubCategoryId);
            if (v.ErrorResult != null)
            {
                return v.ErrorResult;
            }

            this.SetConfirmViewBag(v.Offer!, v.Entry!);
            this.ViewBag.ScopeSubCategoryId = ScopeOf(v.Entry!);
            this.ViewBag.CaptchaRequired = !this.IsCheckoutVerified(directoryEntryId);
            var vm = BuildConfirmationViewModel(v.Offer!, v.Entry!, v.Link2Name!, v.Link3Name!, v.Current!);
            await this.ApplyExistingSponsorPerkToOfferModelAsync(vm.Offer, v.Entry!.DirectoryEntryId).ConfigureAwait(false);
            await this.SetFullPriceViewBagAsync(v.Offer!).ConfigureAwait(false);
            vm.CanCreateSponsoredListing = true;
            return this.View(ConfirmCheckoutView, vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/confirmcheckout")]
        public async Task<IActionResult> ConfirmedCheckoutAsync(
            int directoryEntryId,
            int selectedOfferId,
            string selectedProcessor,
            Guid? rsvId = null,
            string? email = null,
            int? scopeSubCategoryId = null)
        {
            // Buyers who solved the captcha on the previous step aren't asked again. Anyone who
            // reached this form another way (direct post, expired session) still gets a captcha.
            if (!this.IsCheckoutVerified(directoryEntryId)
                && !CaptchaTools.Validate(this.HttpContext, CaptchaContextCheckout, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                return await this.RebuildConfirmViewWithErrorAsync(directoryEntryId, selectedOfferId, rsvId, email, "Captcha", "Incorrect CAPTCHA. Please try again.", scopeSubCategoryId);
            }

            var (entry, offer) = await this.LoadEntryAndOfferAsync(directoryEntryId, selectedOfferId, scopeSubCategoryId);
            if (offer == null || entry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            // Re-check at the step that creates the invoice: the kill-switch may have been turned
            // on, or the request may not have come through the earlier (checked) pages at all.
            var notAllowed = await this.CannotSponsorAsync(entry).ConfigureAwait(false);
            if (notAllowed != null)
            {
                return notAllowed;
            }

            var (group, typeIdForCap) = this.BuildGroupAndCapacity(offer.SponsorshipType, entry);

            var reservationError = await this.HandleReservationForPostAsync(directoryEntryId, offer, rsvId, group, typeIdForCap, ScopeOf(entry));
            if (reservationError != null)
            {
                return reservationError;
            }

            var (emailOk, normalizedEmail, emailView) = await this.ValidateEmailAsync(email, directoryEntryId, selectedOfferId, rsvId, ScopeOf(entry));
            if (!emailOk)
            {
                return emailView!;
            }

            var mainCapError = await this.CheckNewMainSponsorCapAsync(directoryEntryId, offer);
            if (mainCapError != null)
            {
                return this.BadRequest(new { Error = mainCapError });
            }

            // One invoice per solved captcha: going back to confirm again shows the captcha.
            this.HttpContext.Session.Remove(CheckoutVerifiedSessionKeyPrefix + directoryEntryId);

            var invoice = await this.BuildBaseInvoiceAsync(entry, offer);

            if (string.Equals(selectedProcessor, "BTCPayServer", StringComparison.OrdinalIgnoreCase))
            {
                return await this.ExecuteBtcPayCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
            }

            // NOWPayments is retained for legacy/back-compat but is no longer offered in the
            // checkout UI. It only runs when a request explicitly asks for it — never as a default.
            if (string.Equals(selectedProcessor, "NOWPayments", StringComparison.OrdinalIgnoreCase))
            {
                return await this.ExecuteNowPaymentsCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
            }

            // Default (BTCPayServerNoJs and any unrecognized value): the self-hosted no-JS BTCPay flow.
            return await this.ExecuteBtcPayNoJsCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
        }

        // A hot-lead-friendly waiting room shown (instead of a raw error) when the slot a buyer wants is
        // currently being checked out by someone else. No JavaScript: it meta-refreshes every 60s and has
        // a manual "Check again" button. It shows the time left, lets them continue the instant the hold
        // frees, and — if the slot was actually BOUGHT — sends them to the waitlist instead.
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/checkout-waiting")]
        public async Task<IActionResult> CheckoutWaitingAsync(
            int directoryEntryId, SponsorshipType sponsorshipType, int? selectedOfferId = null, int? scopeSubCategoryId = null)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (entry == null || sponsorshipType == SponsorshipType.Unknown)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidSelection });
            }

            if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                entry = await this.ScopeEntryAsync(entry, scopeSubCategoryId).ConfigureAwait(false);
            }

            scopeSubCategoryId = ScopeOf(entry);
            var scopeParam = scopeSubCategoryId.HasValue ? $"&scopeSubCategoryId={scopeSubCategoryId.Value}" : string.Empty;

            var typeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(sponsorshipType, entry, null, null);
            var group = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeId);
            int? typeIdForCap = sponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeId;

            var max = sponsorshipType switch
            {
                SponsorshipType.MainSponsor => Common.Constants.IntegerConstants.MaxMainSponsoredListings,
                SponsorshipType.CategorySponsor => Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
                SponsorshipType.SubcategorySponsor => Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
                _ => 1,
            };

            var paid = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(sponsorshipType, typeIdForCap).ConfigureAwait(false);
            var expiry = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(group).ConfigureAwait(false);
            var reservedNow = expiry.HasValue && expiry.Value > DateTime.UtcNow;

            CheckoutWaitingStatus status;
            if (paid >= max)
            {
                status = CheckoutWaitingStatus.Sold;
            }
            else if (reservedNow)
            {
                status = CheckoutWaitingStatus.Reserved;
            }
            else
            {
                status = CheckoutWaitingStatus.Available;
            }

            var label = sponsorshipType switch
            {
                SponsorshipType.CategorySponsor => $"Category: {entry.SubCategory?.Category?.Name}",
                SponsorshipType.SubcategorySponsor => $"Subcategory: {FormattingHelper.SubcategoryFormatting(entry.SubCategory?.Category?.Name, entry.SubCategory?.Name)}",
                _ => "Main Sponsor",
            };

            var offerParam = selectedOfferId.HasValue ? $"&selectedOfferId={selectedOfferId.Value}" : string.Empty;

            var vm = new CheckoutWaitingViewModel
            {
                Status = status,
                SponsorshipType = sponsorshipType,
                DirectoryEntryId = directoryEntryId,
                SelectedOfferId = selectedOfferId,
                PlacementLabel = label,
                ExpiresUtc = status == CheckoutWaitingStatus.Reserved ? expiry : null,
                MinutesLeft = status == CheckoutWaitingStatus.Reserved && expiry.HasValue
                    ? Math.Max(0, (int)Math.Ceiling((expiry.Value - DateTime.UtcNow).TotalMinutes))
                    : 0,
                RefreshUrl = $"/sponsoredlisting/checkout-waiting?directoryEntryId={directoryEntryId}&sponsorshipType={sponsorshipType}{offerParam}{scopeParam}",
                ContinueUrl = selectedOfferId.HasValue
                    ? $"/sponsoredlisting/confirmcheckout?directoryEntryId={directoryEntryId}&selectedOfferId={selectedOfferId.Value}{scopeParam}"
                    : $"/sponsoredlisting/selectduration?directoryEntryId={directoryEntryId}&sponsorshipType={sponsorshipType}{scopeParam}",
                WaitlistUrl = $"/waitlist?type={sponsorshipType}" + (typeId > 0 ? $"&typeId={typeId}" : string.Empty),
            };

            return this.View("CheckoutWaiting", vm);
        }

        private IActionResult RedirectToCheckoutWaiting(int directoryEntryId, SponsorshipType sponsorshipType, int? selectedOfferId, int? scopeSubCategoryId = null)
        {
            var offerParam = selectedOfferId.HasValue ? $"&selectedOfferId={selectedOfferId.Value}" : string.Empty;
            var scopeParam = scopeSubCategoryId.HasValue ? $"&scopeSubCategoryId={scopeSubCategoryId.Value}" : string.Empty;
            return this.Redirect($"/sponsoredlisting/checkout-waiting?directoryEntryId={directoryEntryId}&sponsorshipType={sponsorshipType}{offerParam}{scopeParam}");
        }

        // =====================================================================
        // NOWPAYMENTS IPN + SUCCESS
        // =====================================================================
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Route("sponsoredlisting/nowpaymentscallback")]
        public async Task<IActionResult> NowPaymentsCallBackAsync()
        {
            var payload = await this.ReadBodyAsync();
            this.logger.LogInformation("NOWPayments IPN received: {Payload}", payload);

            var sig = this.Request.Headers[NowPayments.API.Constants.StringConstants.HeaderNameAuthCallBack].FirstOrDefault() ?? string.Empty;
            if (!this.paymentService.IsIpnRequestValid(payload, sig, out var sigError))
            {
                this.logger.LogWarning("NOWPayments IPN signature invalid: {Error}", sigError);
                return this.Ok();
            }

            var msg = this.DeserializeIpnMessage(payload);
            if (msg?.PaymentStatus == null)
            {
                this.logger.LogWarning("NOWPayments IPN missing PaymentStatus. Payload: {Payload}", payload);
                return this.Ok();
            }

            var invoice = await this.FindInvoiceForIpnAsync(msg);
            if (invoice == null)
            {
                return this.Ok();
            }

            // A NOWPayments callback may only ever touch an invoice that was created with
            // NOWPayments, never a BTCPay one.
            if (invoice.PaymentProcessor != PaymentProcessor.NOWPayments)
            {
                this.logger.LogWarning("NOWPayments IPN ignored: invoice {InvoiceId} was not created with NOWPayments.", invoice.InvoiceId);
                return this.Ok();
            }

            await this.ApplyIpnStatusUpdateAsync(invoice, msg);
            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentssuccess")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string nP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(nP_id);
            if (processorInvoice?.OrderId == null
                || !Guid.TryParse(processorInvoice.OrderId, out var orderGuid))
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                .GetByInvoiceIdAsync(orderGuid)
                .ConfigureAwait(false);
            if (invoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            if (string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId))
            {
                invoice.ProcessorInvoiceId = nP_id;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false);
            }
            else if (!string.Equals(invoice.ProcessorInvoiceId, nP_id, StringComparison.Ordinal))
            {
                this.logger.LogWarning(
                    "NowPaymentsSuccess: NP_id {Np} does not match stored ProcessorInvoiceId {DbId} for order {OrderId}.",
                    nP_id, invoice.ProcessorInvoiceId, invoice.InvoiceId);
                return this.NotFound();
            }

            this.ViewBag.IsPending = invoice.PaymentStatus != PaymentStatus.Paid;
            return this.View("NowPaymentsSuccess", new SuccessViewModel
            {
                OrderId = invoice.InvoiceId,
                ListingEndDate = invoice.CampaignEndDate,
            });
        }

        // =====================================================================
        // BTCPAY WEBHOOK + SUCCESS
        // =====================================================================
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [Route("sponsoredlisting/btcpaycallback")]
        public async Task<IActionResult> BtcPayCallbackAsync()
        {
            this.Request.EnableBuffering();
            var rawBody = await this.ReadBodyAsync(leaveOpen: true);
            this.logger.LogDebug("BTCPay webhook received ({Bytes} bytes).", rawBody?.Length ?? 0);

            var sig = this.Request.Headers["BTCPay-Sig"].FirstOrDefault() ?? string.Empty;
            if (!this.btcPayServerService.IsWebhookValid(rawBody ?? string.Empty, sig, out var sigError))
            {
                this.logger.LogWarning("BTCPay webhook signature invalid: {Error}", sigError);
                return this.Ok();
            }

            BtcPayWebhookPayload? payload;
            try
            {
                payload = JsonConvert.DeserializeObject<BtcPayWebhookPayload>(rawBody);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "BTCPay webhook deserialize failed.");
                return this.Ok();
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.InvoiceId))
            {
                return this.Ok();
            }

            var invoice = await this.sponsoredListingInvoiceRepository.GetByProcessorInvoiceIdAsync(payload.InvoiceId).ConfigureAwait(false);
            if (invoice == null)
            {
                this.logger.LogWarning("BTCPay webhook: invoice not found for {Id}", payload.InvoiceId);
                return this.Ok();
            }

            await this.ApplyBtcPayWebhookUpdateAsync(invoice, payload);
            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/btcpaysuccess")]
        public async Task<IActionResult> BtcPaySuccess([FromQuery] Guid? orderId)
        {
            if (!orderId.HasValue || orderId.Value == Guid.Empty)
            {
                this.logger.LogWarning("BtcPaySuccess reached without a valid orderId.");
                return this.View("BtcPaySuccess", new SuccessViewModel { OrderId = Guid.Empty, ListingEndDate = DateTime.MinValue });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                .GetByInvoiceIdAsync(orderId.Value)
                .ConfigureAwait(false);
            if (invoice == null || invoice.PaymentProcessor != PaymentProcessor.BTCPayServer)
            {
                this.logger.LogWarning("BtcPaySuccess: no BTCPay invoice found for orderId {OrderId}", orderId);
                return this.View("BtcPaySuccess", new SuccessViewModel { OrderId = Guid.Empty, ListingEndDate = DateTime.MinValue });
            }

            // Display-only. PaymentStatus changes happen in the BTCPay webhook
            // (BtcPayCallbackAsync), which is signature-validated and authoritative.
            // Underpayment → dedicated page.
            if (invoice.PaymentStatus == PaymentStatus.UnderPayment)
            {
                return this.View("PartialPayment", new SuccessViewModel
                {
                    OrderId = invoice.InvoiceId,
                    ListingEndDate = invoice.CampaignEndDate,
                });
            }

            this.ViewBag.IsPending = !SponsoredListingCheckoutHelper.IsPaidOrOverpaid(invoice.PaymentStatus);
            return this.View("BtcPaySuccess", new SuccessViewModel
            {
                OrderId = invoice.InvoiceId,
                ListingEndDate = invoice.CampaignEndDate,
            });
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/btcpay-invoice")]
        public async Task<IActionResult> BtcPayNoJsInvoice(string processorInvoiceId, Guid orderId)
        {
            if (string.IsNullOrWhiteSpace(processorInvoiceId) || orderId == Guid.Empty)
            {
                return this.BadRequest(new { Error = "Missing invoice parameters." });
            }

            // Verify the orderId matches what we stored — never trust URL params alone.
            var dbInvoice = await this.sponsoredListingInvoiceRepository
                .GetByInvoiceIdAsync(orderId)
                .ConfigureAwait(false);

            if (dbInvoice == null || dbInvoice.ProcessorInvoiceId != processorInvoiceId)
            {
                return this.NotFound();
            }

            // Already paid in our DB — bounce straight to success without hitting BTCPay.
            if (dbInvoice.PaymentStatus == PaymentStatus.Paid)
            {
                return this.RedirectToAction("BtcPaySuccess", new { orderId });
            }

            var vm = new BtcPayNoJsInvoiceViewModel
            {
                OrderId = orderId,
                ProcessorInvoiceId = processorInvoiceId,
                InvoiceDescription = dbInvoice.InvoiceDescription ?? string.Empty,

                // Safe defaults — page still renders if BTCPay is unreachable.
                Status = "New",
                Address = string.Empty,
                AmountDue = string.Empty,
            };

            // Add this right after:
            var listingEntry = await this.directoryEntryRepository
                .GetByIdAsync(dbInvoice.DirectoryEntryId)
                .ConfigureAwait(false);
            vm.ListingName = listingEntry?.Name ?? string.Empty;

            // Fetch the BTCPay invoice for status + expiry.
            // Failure is non-fatal — page renders in a loading state and auto-refreshes.
            try
            {
                var btcInvoice = await this.btcPayServerService
                    .GetInvoiceAsync(processorInvoiceId)
                    .ConfigureAwait(false);

                if (btcInvoice != null)
                {
                    vm.Status = btcInvoice.Status ?? "New";
                    vm.ExpiresAt = btcInvoice.ExpirationTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(btcInvoice.ExpirationTime).UtcDateTime
                        : (DateTime?)null;

                    // If BTCPay says settled, redirect to the success page.
                    // We don't write PaymentStatus here — that's the webhook's job.
                    // If the webhook hasn't landed yet, the success page will show "pending"
                    // until the webhook updates the DB, then a refresh will show "paid".
                    if (btcInvoice.IsSettled)
                    {
                        return this.RedirectToAction("BtcPaySuccess", new { orderId });
                    }

                    // Expired with partial payment → route through BtcPaySuccess, which now
                    // dispatches UnderPayment to the PartialPayment view.
                    if (dbInvoice.PaymentStatus == PaymentStatus.UnderPayment)
                    {
                        return this.RedirectToAction("BtcPaySuccess", new { orderId });
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "BtcPayNoJsInvoice: could not fetch BTCPay invoice {Id}", processorInvoiceId);
            }

            // Fetch XMR payment method for address, amount, and any detected tx.
            // Failure is non-fatal — page renders with a refresh link.
            try
            {
                var xmrMethod = await this.btcPayServerService
                    .GetXmrPaymentMethodAsync(processorInvoiceId)
                    .ConfigureAwait(false);

                if (xmrMethod != null)
                {
                    vm.Address = xmrMethod.Destination ?? string.Empty;

                    // Use Due when non-zero, fall back to Amount (covers post-payment state
                    // where Due drops to "0" but Amount still holds the original figure).
                    vm.AmountDue = xmrMethod.Due is not(null or "0" or "0.0" or "")
                        ? xmrMethod.Due
                        : xmrMethod.Amount ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(vm.Address) && !string.IsNullOrWhiteSpace(vm.AmountDue))
                    {
                        vm.PaymentUri = $"monero:{vm.Address}?tx_amount={vm.AmountDue}";
                    }

                    // Strip BTCPay's composite key suffix ({txid}#{outputIndex}#{addressIndex})
                    // to expose only the real 64-char Monero transaction ID.
                    if (xmrMethod.Payments?.Count > 0)
                    {
                        var rawId = xmrMethod.Payments[0].Id ?? string.Empty;
                        vm.TxId = rawId.Contains('#') ? rawId.Split('#')[0] : rawId;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "BtcPayNoJsInvoice: could not fetch XMR payment method for {Id}", processorInvoiceId);
            }

            return this.View("BtcPayNoJsInvoice", vm);
        }

        /// <summary>
        /// Returns a QR code PNG for the given BTCPay invoice's monero: payment URI.
        /// Looked up server-side from the processor invoice ID — no URI data in the URL.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/btcpay-qr")]
        public async Task<IActionResult> BtcPayNoJsQr([FromQuery] string processorInvoiceId)
        {
            if (string.IsNullOrWhiteSpace(processorInvoiceId))
            {
                return this.BadRequest();
            }

            string paymentUri;
            try
            {
                var xmrMethod = await this.btcPayServerService.GetXmrPaymentMethodAsync(processorInvoiceId).ConfigureAwait(false);
                if (xmrMethod == null || string.IsNullOrWhiteSpace(xmrMethod.Destination))
                {
                    return this.NotFound();
                }

                var amount = xmrMethod.Due ?? xmrMethod.Amount ?? string.Empty;
                paymentUri = string.IsNullOrWhiteSpace(amount)
                    ? $"monero:{xmrMethod.Destination}"
                    : $"monero:{xmrMethod.Destination}?tx_amount={amount}";
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "BtcPayNoJsQr: could not fetch payment method for {Id}", processorInvoiceId);
                return this.NotFound();
            }

            // Generate QR using QRCoder (NuGet: QRCoder).
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(paymentUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrData);
            var pngBytes = qrCode.GetGraphic(6); // 6 pixels per module → ~240px for a typical XMR address

            return this.File(pngBytes, "image/png");
        }

        // =====================================================================
        // PRIVATE: No-JS checkout orchestration
        // =====================================================================
        private async Task<IActionResult> ExecuteBtcPayNoJsCheckoutAsync(
            SponsoredListingInvoice invoice, SponsoredListingOffer offer, Guid? rsvId, string? normalizedEmail)
        {
            var req = new BtcPayInvoiceRequest
            {
                Amount = invoice.Amount.ToString("0.00"),
                Currency = this.btcPayServerService.DefaultCurrency,
                Metadata = new Dictionary<string, object>
                {
                    ["orderId"] = invoice.InvoiceId.ToString(),
                    ["itemDesc"] = offer.Description,
                },
                Checkout = new BtcPayCheckoutOptions
                {
                    // RedirectUrl still set so the success page works if the user navigates away.
                    RedirectUrl = $"{this.btcPayServerService.SuccessUrl}?orderId={invoice.InvoiceId}",
                    RedirectAutomatically = false,
                    DefaultPaymentMethod = Currency.XMR.ToString(),
                },
            };

            BtcPayInvoiceResponse btcPayInvoice;
            try
            {
                btcPayInvoice = await this.btcPayServerService.CreateInvoiceAsync(req).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "BTCPay no-JS invoice creation failed for order {OrderId}", invoice.InvoiceId);
                return this.BadRequest(new { Error = "Failed to create BTCPay invoice." });
            }

            invoice.ReservationGuid = rsvId ?? Guid.Empty;
            invoice.ProcessorInvoiceId = btcPayInvoice.Id;
            invoice.PaymentProcessor = PaymentProcessor.BTCPayServer;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(req);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(btcPayInvoice);
            invoice.Email = InputHelper.SetEmail(normalizedEmail);

            // XMR-only BTCPay store: record the payment currency + capture BTCPay's actual locked
            // rate (USD per XMR) now, so the details view shows "Monero" and the real rate at
            // creation — not just once paid. Best-effort; must never block checkout.
            invoice.PaidInCurrency = Currency.XMR;
            await this.CaptureBtcPayRateAsync(invoice, btcPayInvoice.Id).ConfigureAwait(false);

            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false))
            {
                return this.InvoiceSaveFailed(invoice);
            }

            return this.RedirectToAction("BtcPayNoJsInvoice", new
            {
                processorInvoiceId = btcPayInvoice.Id,
                orderId = invoice.InvoiceId,
            });
        }

        // =====================================================================
        // CHECKOUT ORCHESTRATION HELPERS
        // =====================================================================
        private async Task<ConfirmValidationResult> ValidateConfirmRequestAsync(int directoryEntryId, int selectedOfferId, Guid? rsvId, int? scopeSubCategoryId = null)
        {
            var (entry, scopedOffer) = await this.LoadEntryAndOfferAsync(directoryEntryId, selectedOfferId, scopeSubCategoryId).ConfigureAwait(false);
            if (entry == null)
            {
                return ConfirmValidationResult.Fail(this.BadRequest(new { Error = StringConstants.InvalidSelection }));
            }

            // Same eligibility rule as the first checkout steps, re-checked here so nobody can skip
            // straight to the final confirm for a listing that may not sponsor.
            if (!this.IsEligibleToSponsor(entry)
                && !await this.sponsoredListingRepository.HasActiveSponsorshipAsync(entry.DirectoryEntryId).ConfigureAwait(false))
            {
                return ConfirmValidationResult.Fail(this.BadRequest(new { Error = $"To sponsor, a listing must be Verified and listed in the directory for at least {DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring} days." }));
            }

            var offer = scopedOffer;
            if (offer == null)
            {
                return ConfirmValidationResult.Fail(this.BadRequest(new { Error = StringConstants.InvalidOfferSelection }));
            }

            // Subcategory sponsorships in an additional subcategory are checked against that subcategory.
            var subcatBlocked = this.BlockedBySubcategorySponsorship(entry);
            if (subcatBlocked != null)
            {
                return ConfirmValidationResult.Fail(subcatBlocked);
            }

            var (group, typeIdForCap) = this.BuildGroupAndCapacity(offer.SponsorshipType, entry);
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActiveInScopeAsync(directoryEntryId, offer.SponsorshipType, typeIdForCap).ConfigureAwait(false);

            if (!isExtension && offer.SponsorshipType == SponsorshipType.MainSponsor)
            {
                var capError = await this.CheckMainSubcategoryCapAsync(entry.SubCategoryId);
                if (capError != null)
                {
                    return ConfirmValidationResult.Fail(this.BadRequest(new { Error = capError }));
                }
            }

            if (!isExtension && !await this.TryAttachReservationAsync(rsvId, group, directoryEntryId))
            {
                var capacityError = await this.CheckListingCapacityAsync(offer.SponsorshipType, typeIdForCap, group);
                if (capacityError != null)
                {
                    return ConfirmValidationResult.Fail(this.RedirectToCheckoutWaiting(directoryEntryId, offer.SponsorshipType, offer.SponsoredListingOfferId, ScopeOf(entry)));
                }
            }

            var l2 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var l3 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var current = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(offer.SponsorshipType).ConfigureAwait(false);
            return ConfirmValidationResult.Ok(offer, entry, l2, l3, current);
        }

        // Confirm page: the "full price" for this length at the shortest option's per-day rate (the same
        // price list the offer came from), so the length discount can be shown struck through.
        private async Task SetFullPriceViewBagAsync(SponsoredListingOffer offer)
        {
            int? placementId = offer.SponsorshipType switch
            {
                SponsorshipType.CategorySponsor => offer.CategoryId,
                SponsorshipType.SubcategorySponsor => offer.SubcategoryId,
                _ => null,
            };
            var offers = await this.sponsoredListingOfferRepository
                .GetOffersForPlacementAsync(offer.SponsorshipType, placementId).ConfigureAwait(false);
            var shortest = offers.Where(o => o.Days > 0).OrderBy(o => o.Days).FirstOrDefault();
            if (shortest != null && offer.Days > 0)
            {
                this.ViewBag.FullPriceUsd = Math.Round(shortest.Price / shortest.Days * offer.Days, 2);
            }
        }

        private void SetConfirmViewBag(SponsoredListingOffer offer, DirectoryEntry entry)
        {
            var typeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, entry, null, null);
            if (offer.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.SubCategoryId = typeId;
            }
            else if (offer.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.CategoryId = typeId;
            }

            // The exact scope the buyer is purchasing, shown on the confirm page under Sponsorship Type
            // (Category → the category; Subcategory → full "Category » Subcategory"; Main → n/a).
            this.ViewBag.PurchaseCategoryName = entry.SubCategory?.Category?.Name;
            this.ViewBag.PurchaseSubcategoryScope = FormattingHelper.SubcategoryFormatting(
                entry.SubCategory?.Category?.Name, entry.SubCategory?.Name);
        }

        private async Task<IActionResult> RebuildConfirmViewWithErrorAsync(
            int directoryEntryId, int selectedOfferId, Guid? rsvId,
            string? prefillEmail, string key, string message, int? scopeSubCategoryId = null)
        {
            var (entry, offer) = await this.LoadEntryAndOfferAsync(directoryEntryId, selectedOfferId, scopeSubCategoryId);
            if (offer == null || entry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var typeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, entry, null, null);
            if (offer.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.SubCategoryId = typeId;
            }
            else if (offer.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.CategoryId = typeId;
            }

            var l2 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var l3 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var cur = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(offer.SponsorshipType);

            await this.TryAttachReservationAsync(rsvId, ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeId), directoryEntryId);

            var vm = BuildConfirmationViewModel(offer, entry, l2, l3, cur);
            await this.ApplyExistingSponsorPerkToOfferModelAsync(vm.Offer, entry.DirectoryEntryId).ConfigureAwait(false);
            await this.SetFullPriceViewBagAsync(offer).ConfigureAwait(false);
            vm.CanCreateSponsoredListing = true;
            this.ModelState.AddModelError(key, message);
            this.ViewBag.PrefillEmail = prefillEmail;
            this.ViewBag.ScopeSubCategoryId = ScopeOf(entry);
            this.ViewBag.CaptchaRequired = !this.IsCheckoutVerified(directoryEntryId);
            return this.View(ConfirmCheckoutView, vm);
        }

        private async Task<IActionResult?> HandleReservationForPostAsync(
            int directoryEntryId, SponsoredListingOffer offer, Guid? rsvId, string group, int? typeIdForCap, int? scopeSubCategoryId = null)
        {
            if (!await this.TryAttachReservationAsync(rsvId, group, directoryEntryId))
            {
                var isActive = await this.sponsoredListingRepository.IsSponsoredListingActiveInScopeAsync(directoryEntryId, offer.SponsorshipType, typeIdForCap);
                var totalActive = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(offer.SponsorshipType, typeIdForCap);
                var totalReserved = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(group);

                if (!SponsoredListingCheckoutHelper.CanPurchaseListing(totalActive, totalReserved, offer.SponsorshipType) && !isActive)
                {
                    return this.RedirectToCheckoutWaiting(directoryEntryId, offer.SponsorshipType, offer.SponsoredListingOfferId, scopeSubCategoryId);
                }
            }
            else
            {
                await this.CancelStaleInvoiceOnReservationAsync(rsvId!.Value);
            }

            return null;
        }

        private async Task CancelStaleInvoiceOnReservationAsync(Guid rsvId)
        {
            var reservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId);
            if (reservation == null)
            {
                return;
            }

            var existing = await this.sponsoredListingInvoiceRepository.GetByReservationGuidAsync(reservation.ReservationGuid);
            // Only an untouched invoice is stale. Once a payment has been seen (awaiting
            // confirmations, under/over-paid, paid) it must never be canceled from here, or the
            // buyer pays and nothing activates.
            if (existing == null
                || existing.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Pending
                    or PaymentStatus.UnderPayment or PaymentStatus.OverPayment or PaymentStatus.Refunded)
            {
                return;
            }

            existing.PaymentStatus = PaymentStatus.Canceled;
            existing.ReservationGuid = Guid.Empty;
            await this.sponsoredListingInvoiceRepository.UpdateAsync(existing);
        }

        private async Task<(bool ok, string? normalized, IActionResult? errorView)> ValidateEmailAsync(
            string? email, int directoryEntryId, int selectedOfferId, Guid? rsvId, int? scopeSubCategoryId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return (true, null, null);
            }

            var (ok, norm, emailError) = EmailValidationHelper.Validate(email);
            if (ok)
            {
                return (true, norm!, null);
            }

            return (false, null, await this.RebuildConfirmViewWithErrorAsync(directoryEntryId, selectedOfferId, rsvId, email, "Email", emailError!, scopeSubCategoryId));
        }

        private async Task<string?> CheckNewMainSponsorCapAsync(int directoryEntryId, SponsoredListingOffer offer)
        {
            if (offer.SponsorshipType != SponsorshipType.MainSponsor)
            {
                return null;
            }

            var isActive = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType).ConfigureAwait(false);
            if (isActive)
            {
                return null;
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (entry == null)
            {
                return null;
            }

            if (!await this.CanPurchaseMainWithinSubcategoryAsync(entry.SubCategoryId).ConfigureAwait(false))
            {
                return await this.BuildMainSubcategoryLimitMessageAsync(entry.SubCategoryId).ConfigureAwait(false);
            }

            return null;
        }

        private async Task<SponsoredListingInvoice> BuildBaseInvoiceAsync(
            DirectoryEntry entry, SponsoredListingOffer offer)
        {
            // An extension starts when the current campaign in the SAME (sub)category ends.
            var scopeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, entry, null, null);
            var existing = await this.sponsoredListingRepository.GetActiveSponsorInScopeAsync(entry.DirectoryEntryId, offer.SponsorshipType, scopeId);
            var startDate = existing?.CampaignEndDate ?? DateTime.UtcNow;
            return await this.CreateInvoiceAsync(entry, offer, startDate);
        }

        private async Task<IActionResult> ExecuteNowPaymentsCheckoutAsync(
            SponsoredListingInvoice invoice, SponsoredListingOffer offer, Guid? rsvId, string? normalizedEmail)
        {
            var req = new PaymentRequest
            {
                IsFeePaidByUser = false,
                PriceAmount = invoice.Amount,
                PriceCurrency = this.paymentService.PriceCurrency,
                PayCurrency = this.paymentService.PayCurrency,
                OrderId = invoice.InvoiceId.ToString(),
                OrderDescription = offer.Description,
            };
            this.paymentService.SetDefaultUrls(req);

            var processorInvoice = await this.paymentService.CreateInvoice(req);
            if (processorInvoice?.Id == null)
            {
                return this.BadRequest(new { Error = "Failed to create NOWPayments invoice." });
            }

            invoice.ReservationGuid = rsvId ?? Guid.Empty;
            invoice.ProcessorInvoiceId = processorInvoice.Id;
            invoice.PaymentProcessor = PaymentProcessor.NOWPayments;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(req);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(processorInvoice);
            invoice.Email = InputHelper.SetEmail(normalizedEmail);
            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                return this.InvoiceSaveFailed(invoice);
            }

            if (string.IsNullOrWhiteSpace(processorInvoice.InvoiceUrl))
            {
                return this.BadRequest(new { Error = "Failed to get invoice URL." });
            }

            return this.Redirect(processorInvoice.InvoiceUrl);
        }

        private async Task<IActionResult> ExecuteBtcPayCheckoutAsync(
            SponsoredListingInvoice invoice, SponsoredListingOffer offer, Guid? rsvId, string? normalizedEmail)
        {
            var req = new BtcPayInvoiceRequest
            {
                Amount = invoice.Amount.ToString("0.00"),
                Currency = this.btcPayServerService.DefaultCurrency,
                Metadata = new Dictionary<string, object>
                {
                    ["orderId"] = invoice.InvoiceId.ToString(),
                    ["itemDesc"] = offer.Description,
                },
                Checkout = new BtcPayCheckoutOptions
                {
                    RedirectUrl = $"{this.btcPayServerService.SuccessUrl}?orderId={invoice.InvoiceId}",
                    RedirectAutomatically = true,
                    DefaultPaymentMethod = Currency.XMR.ToString(),
                },
            };

            BtcPayInvoiceResponse btcPayInvoice;
            try
            {
                btcPayInvoice = await this.btcPayServerService.CreateInvoiceAsync(req).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "BTCPay invoice creation failed for order {OrderId}", invoice.InvoiceId);
                return this.BadRequest(new { Error = "Failed to create BTCPay invoice." });
            }

            invoice.ReservationGuid = rsvId ?? Guid.Empty;
            invoice.ProcessorInvoiceId = btcPayInvoice.Id;
            invoice.PaymentProcessor = PaymentProcessor.BTCPayServer;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(req);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(btcPayInvoice);
            invoice.Email = InputHelper.SetEmail(normalizedEmail);

            // This invoice is denominated for Monero payment (the BTCPay store is XMR-only), so
            // record the payment currency up front — the details view then shows "Monero" at
            // creation, not just once paid.
            invoice.PaidInCurrency = Currency.XMR;

            // Capture the ACTUAL exchange rate BTCPay locked for this invoice (USD per XMR) so the
            // details view shows the real rate, not one implied from the amount paid. BTCPay
            // computes the per-method rate a moment AFTER creation, so this retries briefly.
            // Best-effort — a rate hiccup must never block checkout.
            await this.CaptureBtcPayRateAsync(invoice, btcPayInvoice.Id).ConfigureAwait(false);

            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                return this.InvoiceSaveFailed(invoice);
            }

            return this.Redirect(btcPayInvoice.CheckoutLink);
        }

        // =====================================================================
        // NOWPAYMENTS IPN HELPERS
        // =====================================================================
        private IpnPaymentMessage? DeserializeIpnMessage(string payload)
        {
            try
            {
                return JsonConvert.DeserializeObject<IpnPaymentMessage>(payload);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "NOWPayments IPN deserialize failed.");
                return null;
            }
        }

        private async Task<SponsoredListingInvoice?> FindInvoiceForIpnAsync(IpnPaymentMessage msg)
        {
            SponsoredListingInvoice? invoice = null;
            var hasOrderGuid = Guid.TryParse(msg.OrderId, out var orderGuid);
            if (hasOrderGuid)
            {
                invoice = await this.sponsoredListingInvoiceRepository.GetByInvoiceIdAsync(orderGuid);
            }

            var ipnProcessorId = msg.InvoiceId.ToString() ?? string.Empty;
            if (invoice == null && !string.IsNullOrWhiteSpace(ipnProcessorId))
            {
                invoice = await this.sponsoredListingInvoiceRepository.GetByProcessorInvoiceIdAsync(ipnProcessorId);
                if (invoice != null && hasOrderGuid && invoice.InvoiceId != orderGuid)
                {
                    this.logger.LogWarning("IPN processor_id match but OrderId mismatch. IPN:{A} DB:{B}", msg.OrderId, invoice.InvoiceId);
                    return null;
                }
            }

            if (invoice == null)
            {
                this.logger.LogWarning("NOWPayments IPN: invoice not found. OrderId:{A} ProcessorId:{B}", msg.OrderId, ipnProcessorId);
                return null;
            }

            if (hasOrderGuid && invoice.InvoiceId != orderGuid)
            {
                this.logger.LogWarning("NOWPayments IPN: OrderId mismatch. IPN:{A} DB:{B}", msg.OrderId, invoice.InvoiceId);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId) &&
                !string.IsNullOrWhiteSpace(ipnProcessorId) &&
                !string.Equals(invoice.ProcessorInvoiceId, ipnProcessorId, StringComparison.Ordinal))
            {
                this.logger.LogWarning("NOWPayments IPN: processor_id mismatch. OrderId:{A} DB:{B} IPN:{C}", msg.OrderId, invoice.ProcessorInvoiceId, ipnProcessorId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId) && !string.IsNullOrWhiteSpace(ipnProcessorId))
            {
                invoice.ProcessorInvoiceId = ipnProcessorId;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
            }

            return invoice;
        }

        private async Task ApplyIpnStatusUpdateAsync(SponsoredListingInvoice invoice, IpnPaymentMessage msg)
        {
            var proposed = SponsoredListingCheckoutHelper.ConvertToInternalStatus(
                EnumHelper.ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(msg.PaymentStatus));

            if (!this.ShouldProcessPaymentEvent(invoice, proposed))
            {
                return;
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(msg);
            invoice.PaidAmount = msg.PayAmount;
            invoice.OutcomeAmount = msg.OutcomeAmount;
            if (!string.IsNullOrWhiteSpace(msg.PayCurrency))
            {
                invoice.PaidInCurrency = EnumHelper.ParseStringToEnum<Currency>(msg.PayCurrency);
            }

            if (SponsoredListingCheckoutHelper.CanAdvanceStatus(invoice.PaymentStatus, proposed))
            {
                this.LogLatePayment(invoice, proposed);
                invoice.PaymentStatus = proposed;
            }
            else if (proposed != invoice.PaymentStatus)
            {
                this.logger.LogInformation("Ignoring status change on invoice {Id}: {Cur} -> {Proposed}", invoice.InvoiceId, invoice.PaymentStatus, proposed);
            }

            // NOTE: the slot hold is a FIXED 15-minute window and is deliberately NOT extended by
            // invoice creation or payment status — an unpaid (or slowly-paid) invoice must never keep a
            // slot held beyond that window. Only a settled payment permanently consumes the slot.
            // Throws if a save fails, so the IPN answers 500 and NOWPayments retries.
            await this.activator.SaveAndActivateAsync(invoice);
        }

        // =====================================================================
        // BTCPAY HELPERS
        // =====================================================================
        private async Task ApplyBtcPayWebhookUpdateAsync(SponsoredListingInvoice invoice, BtcPayWebhookPayload payload)
        {
            var proposed = SponsoredListingCheckoutHelper.MapBtcPayEventToInternalStatus(
                payload.Type,
                partiallyPaid: payload.PartiallyPaid);

            if (!this.ShouldProcessPaymentEvent(invoice, proposed))
            {
                return;
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(payload);

            // Surface overpayments to the log even though we now treat them as Paid.
            // The full payload is in invoice.PaymentResponse and BTCPay shows the real amounts.
            if (payload.OverPaid)
            {
                this.logger.LogInformation(
                    "BTCPay reported OverPaid=true for processor invoice {ProcessorId} (order {OrderId}). " +
                    "Treating as Paid. Inspect BTCPay or invoice.PaymentResponse for actual amounts.",
                    payload.InvoiceId,
                    invoice.InvoiceId);
            }

            if (SponsoredListingCheckoutHelper.CanAdvanceStatus(invoice.PaymentStatus, proposed))
            {
                this.LogLatePayment(invoice, proposed);
                invoice.PaymentStatus = proposed;
            }

            // Capture XMR amount whenever we're at or past Pending — covers Pending, UnderPayment,
            // Paid, and any OverPayment that might be set by another code path. InvoiceReceivedPayment
            // and InvoicePaymentSettled carry payment.value directly; InvoiceSettled is aggregate
            // and needs the payment-methods endpoint.
            if (invoice.PaymentStatus is PaymentStatus.Pending
                                      or PaymentStatus.UnderPayment
                                      or PaymentStatus.Paid
                                      or PaymentStatus.OverPayment)
            {
                var ns = System.Globalization.NumberStyles.Any;
                var ci = System.Globalization.CultureInfo.InvariantCulture;

                if (payload.Payment != null
                    && !string.IsNullOrWhiteSpace(payload.Payment.Value)
                    && decimal.TryParse(payload.Payment.Value, ns, ci, out var webhookXmr)
                    && webhookXmr > 0m)
                {
                    invoice.PaidAmount = webhookXmr;
                    invoice.OutcomeAmount = webhookXmr;
                    invoice.PaidInCurrency = Currency.XMR;
                }
                else
                {
                    await this.PopulateBtcPayPaymentAmountsAsync(invoice, payload.InvoiceId);
                }
            }

            // NOTE: the slot hold is a FIXED 15-minute window and is deliberately NOT extended by
            // invoice creation or payment status — an unpaid (or slowly-paid) invoice must never keep a
            // slot held beyond that window. Only a settled payment permanently consumes the slot.
            // Throws if a save fails, so the webhook answers 500 and BTCPay redelivers it.
            await this.activator.SaveAndActivateAsync(invoice);
        }

        // A payment event is applied when it moves the status forward, or when the invoice is
        // already paid but its listing was never created (an earlier delivery failed part-way):
        // the redelivery then finishes the activation. Anything else on a final invoice is ignored.
        private bool ShouldProcessPaymentEvent(SponsoredListingInvoice invoice, PaymentStatus proposed)
        {
            if (!SponsoredListingCheckoutHelper.IsTerminal(invoice.PaymentStatus))
            {
                return true;
            }

            return SponsoredListingCheckoutHelper.CanAdvanceStatus(invoice.PaymentStatus, proposed)
                || this.activator.NeedsActivation(invoice);
        }

        private void LogLatePayment(SponsoredListingInvoice invoice, PaymentStatus proposed)
        {
            if (invoice.PaymentStatus is PaymentStatus.Canceled or PaymentStatus.Expired)
            {
                this.logger.LogWarning(
                    "Invoice {InvoiceId} was {Status} locally but the processor reports {Proposed}; activating it because the buyer paid. Check the slot is not over capacity.",
                    invoice.InvoiceId, invoice.PaymentStatus, proposed);
            }
        }

        private IActionResult InvoiceSaveFailed(SponsoredListingInvoice invoice)
        {
            this.logger.LogError("Could not save processor details for sponsorship invoice {InvoiceId}; checkout stopped before payment.", invoice.InvoiceId);
            return this.StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "We could not save your order, so nothing was charged. Please go back and try again." });
        }

        /// <summary>
        /// Captures BTCPay's quoted exchange rate for the XMR payment method (invoice currency per
        /// 1 XMR, e.g. USD per XMR) and stores it on the invoice. Called at invoice creation so the
        /// details view can show the REAL rate, not one implied from Amount/PaidAmount. Best-effort.
        /// </summary>
        private async Task CaptureBtcPayRateAsync(SponsoredListingInvoice invoice, string processorInvoiceId)
        {
            var ns = System.Globalization.NumberStyles.Any;
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            try
            {
                // 1. Preferred: the rate BTCPay locked onto THIS invoice's XMR payment method.
                //    But our store uses lazy payment methods, so that method (and its rate) isn't
                //    generated until the buyer opens checkout — at creation it reads 0. One immediate
                //    attempt only; the old multi-second backoff just slowed every checkout for nothing.
                var xmr = await this.btcPayServerService.GetXmrPaymentMethodAsync(processorInvoiceId).ConfigureAwait(false);
                if (xmr != null
                    && decimal.TryParse(xmr.Rate, ns, ci, out var rate)
                    && rate > 0m)
                {
                    invoice.PaymentRate = rate;
                    return;
                }

                // 2. Fallback (the normal case at creation): the store's current XMR/USD rate, pulled
                //    straight from the same rate source BTCPay locks from — so it matches the invoice's
                //    locked rate to the second of creation and is shown on the details view right away.
                var spot = await this.btcPayServerService.GetXmrRateAsync("USD").ConfigureAwait(false);
                if (spot > 0m)
                {
                    invoice.PaymentRate = spot;
                    return;
                }

                this.logger.LogWarning(
                    "CaptureBtcPayRate: no XMR rate available for invoice {Id} at creation; " +
                    "will be captured on the first payment webhook instead.",
                    processorInvoiceId);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not capture BTCPay exchange rate for invoice {Id}", processorInvoiceId);
            }
        }

        /// <summary>
        /// Fetches the XMR amount paid from the BTCPay payment-methods endpoint and writes it
        /// into PaidAmount, OutcomeAmount, and PaidInCurrency.
        /// Both amounts are the XMR crypto value — the USD invoice amount is already in
        /// invoice.Amount and never changes.
        /// Falls back through paymentMethodPaid → totalPaid → amount because BTCPay may
        /// populate different fields depending on invoice state and Monero confirmation stage.
        /// Failures are swallowed — amounts are non-critical for listing activation.
        /// </summary>
        private async Task PopulateBtcPayPaymentAmountsAsync(SponsoredListingInvoice invoice, string processorInvoiceId)
        {
            try
            {
                var xmrMethod = await this.btcPayServerService.GetXmrPaymentMethodAsync(processorInvoiceId).ConfigureAwait(false);
                if (xmrMethod == null)
                {
                    this.logger.LogWarning("PopulateBtcPayPaymentAmounts: no XMR payment method found for {Id}", processorInvoiceId);
                    return;
                }

                var ns = System.Globalization.NumberStyles.Any;
                var ci = System.Globalization.CultureInfo.InvariantCulture;

                // Capture the payment currency + BTCPay's actual quoted rate (USD per XMR) FIRST —
                // these are available as soon as the invoice exists, independent of any payment, so
                // do it BEFORE the no-payment early return below. This is the real rate (not the one
                // implied by Amount/PaidAmount) and it also backfills invoices created before the
                // rate was captured at checkout.
                invoice.PaidInCurrency = Currency.XMR;
                if (decimal.TryParse(xmrMethod.Rate, ns, ci, out var xmrRate) && xmrRate > 0m)
                {
                    invoice.PaymentRate = xmrRate;
                }

                var xmrPaid = 0m;

                // Try paymentMethodPaid first, fall back to totalPaid.
                foreach (var candidate in new[] { xmrMethod.PaymentMethodPaid, xmrMethod.TotalPaid })
                {
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && decimal.TryParse(candidate, ns, ci, out var parsed)
                        && parsed > 0m)
                    {
                        xmrPaid = parsed;
                        break;
                    }
                }

                if (xmrPaid == 0m)
                {
                    this.logger.LogWarning(
                        "PopulateBtcPayPaymentAmounts: could not resolve XMR amount for {Id}. " +
                        "paymentMethodPaid={A} totalPaid={B}",
                        processorInvoiceId,
                        xmrMethod.PaymentMethodPaid,
                        xmrMethod.TotalPaid);
                    return;
                }

                invoice.PaidAmount = xmrPaid;
                invoice.OutcomeAmount = xmrPaid;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not fetch payment amounts from BTCPay for invoice {Id}", processorInvoiceId);
            }
        }

        // =====================================================================
        // RESERVATION HELPERS
        // =====================================================================
        private async Task<Guid?> ValidateExistingReservationAsync(Guid? rsvId, string reservationGroup, int directoryEntryId = 0)
        {
            // No rsvId in the URL → recover this buyer's own hold for THIS listing + slot from the DB, so a
            // checkout URL without the guid (e.g. browser-back to the bare select-duration URL) still reuses
            // their reservation instead of treating them as a new buyer. Matched by entry, so no one can
            // piggyback on someone else's hold.
            rsvId ??= await this.sponsoredListingReservationRepository
                .GetActiveReservationGuidForEntryAsync(directoryEntryId, reservationGroup).ConfigureAwait(false);
            if (!rsvId.HasValue)
            {
                return null;
            }

            var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value).ConfigureAwait(false);
            return existing != null && existing.ReservationGroup == reservationGroup && existing.ExpirationDateTime > DateTime.UtcNow ? rsvId : null;
        }

        private async Task<string?> CheckListingCapacityAsync(SponsorshipType type, int? typeIdForCap, string group)
        {
            var total = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(type, typeIdForCap).ConfigureAwait(false);
            var reserved = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(group).ConfigureAwait(false);
            return SponsoredListingCheckoutHelper.CanPurchaseListing(total, reserved, type) ? null : await this.BuildCheckoutInProcessMessageAsync(type, typeIdForCap, group);
        }

        private async Task<string?> CheckMainSubcategoryCapAsync(int subCategoryId)
        {
            var active = await this.GetActiveMainCountForSubcategoryAsync(subCategoryId).ConfigureAwait(false);
            return active >= Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory
                ? await this.BuildMainSubcategoryLimitMessageAsync(subCategoryId).ConfigureAwait(false)
                : null;
        }

        private async Task<string?> CheckMainSubcategoryReservationAsync(int subCategoryId)
        {
            var subGroup = ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(subCategoryId);
            var count = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(subGroup).ConfigureAwait(false);
            return count > 0 ? await this.BuildCheckoutInProcessMessageForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false) : null;
        }

        private async Task<Guid> CreateReservationGuidAsync(SponsorshipType type, DirectoryEntry entry, int? subCategoryId, int? categoryId)
        {
            var typeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(type, entry, subCategoryId, categoryId);
            var group = ReservationGroupHelper.BuildReservationGroupName(type, typeId);
            var details = await this.BuildReservationDetailsAsync(type, entry, subCategoryId, categoryId);
            var res = await this.sponsoredListingReservationRepository.CreateReservationAsync(DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes), group, details).ConfigureAwait(false);
            return res.ReservationGuid;
        }

        private async Task CreateShadowSubcategoryReservationAsync(int subCategoryId, DirectoryEntry entry, Guid parentGuid)
        {
            var parent = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(parentGuid);
            if (parent == null)
            {
                return;
            }

            var subGroup = ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(subCategoryId);
            var details = await this.BuildReservationDetailsAsync(SponsorshipType.MainSponsor, entry, subCategoryId, null);
            await this.sponsoredListingReservationRepository.CreateReservationAsync(parent.ExpirationDateTime, subGroup, details).ConfigureAwait(false);
        }

        // =====================================================================
        // STATIC VIEW MODEL BUILDER
        // =====================================================================
        private static ConfirmSelectionViewModel BuildConfirmationViewModel(
            SponsoredListingOffer offer, DirectoryEntry de, string l2, string l3, IEnumerable<SponsoredListing> current) =>
            new ConfirmSelectionViewModel
            {
                SelectedDirectoryEntry = new DirectoryEntryViewModel
                {
                    CreateDate = de.CreateDate,
                    UpdateDate = de.UpdateDate,
                    ItemDisplayType = DisplayFormatting.Enums.ItemDisplayType.Normal,
                    DateOption = DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                    IsSponsored = false,
                    Link2Name = l2,
                    Link3Name = l3,
                    Link = de.Link,
                    Name = de.Name,
                    DirectoryEntryKey = de.DirectoryEntryKey,
                    Description = de.Description,
                    DirectoryEntryId = de.DirectoryEntryId,
                    DirectoryStatus = de.DirectoryStatus,
                    Link2 = de.Link2,
                    Link3 = de.Link3,
                    Location = de.Location,
                    Note = de.Note,
                    Processor = de.Processor,
                    SubCategoryId = de.SubCategoryId,
                    CountryCode = de.CountryCode,
                    PgpKey = de.PgpKey,
                },
                Offer = new SponsoredListingOfferModel
                {
                    Description = offer.Description,
                    Days = offer.Days,
                    SponsoredListingOfferId = offer.SponsoredListingOfferId,
                    USDPrice = offer.Price,
                    SponsorshipType = offer.SponsorshipType,
                },
                // Only an active campaign in THIS listing's current (sub)category counts as an extension.
                IsExtension = current.Any(x => x.DirectoryEntryId == de.DirectoryEntryId &&
                    SponsoredListingCheckoutHelper.IsSameScope(
                        x,
                        offer.SponsorshipType,
                        SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, de, null, null))),
            };

        // =====================================================================
        // INSTANCE HELPERS
        // =====================================================================
        private (string group, int? typeIdForCapacity) BuildGroupAndCapacity(SponsorshipType type, DirectoryEntry entry)
        {
            var typeId = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(type, entry, null, null);
            return (ReservationGroupHelper.BuildReservationGroupName(type, typeId), type == SponsorshipType.MainSponsor ? (int?)null : typeId);
        }

        private void SetSponsorshipScopeViewBag(SponsorshipType type, DirectoryEntry entry, int typeIdForGroup)
        {
            if (type == SponsorshipType.SubcategorySponsor)
            {
                this.ViewBag.Subcategory = FormattingHelper.SubcategoryFormatting(entry.SubCategory?.Category?.Name, entry.SubCategory?.Name);
                this.ViewBag.SubCategoryId = entry.SubCategoryId;
            }
            else if (type == SponsorshipType.CategorySponsor)
            {
                this.ViewBag.Category = entry.SubCategory?.Category?.Name;
                this.ViewBag.CategoryId = typeIdForGroup;
            }
        }

        private void MarkCheckoutVerified(int directoryEntryId) =>
            this.HttpContext.Session.SetString(
                CheckoutVerifiedSessionKeyPrefix + directoryEntryId,
                DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        private bool IsCheckoutVerified(int directoryEntryId)
        {
            var raw = this.HttpContext.Session.GetString(CheckoutVerifiedSessionKeyPrefix + directoryEntryId);
            return long.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ticks)
                && DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) < CheckoutVerifiedLifetime;
        }

        private async Task<string> ReadBodyAsync(bool leaveOpen = false)
        {
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8, leaveOpen: leaveOpen);
            return await reader.ReadToEndAsync();
        }

        private async Task<bool> TryAttachReservationAsync(Guid? rsvId, string reservationGroup, int directoryEntryId = 0)
        {
            // No rsvId in the URL → recover this buyer's own hold for THIS listing + slot from the DB.
            rsvId ??= await this.sponsoredListingReservationRepository
                .GetActiveReservationGuidForEntryAsync(directoryEntryId, reservationGroup).ConfigureAwait(false);
            if (rsvId == null)
            {
                return false;
            }

            var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value).ConfigureAwait(false);
            if (existing == null)
            {
                return false;
            }

            // The hold must be for THIS slot and still running. (This used to read a non-existent
            // "ExpirationDate" property by reflection, so any GUID ever issued was accepted while
            // someone else held the slot.)
            if (!string.Equals(existing.ReservationGroup, reservationGroup, StringComparison.Ordinal))
            {
                return false;
            }

            DateTime? expiresUtc = existing.ExpirationDateTime;

            if (!expiresUtc.HasValue || expiresUtc.Value <= DateTime.UtcNow)
            {
                return false;
            }

            this.ViewBag.ReservationGuid = rsvId;
            this.ViewBag.ReservationExpiresUtc = expiresUtc.Value;
            return true;
        }

        // Per-subcategory sponsorship kill-switch. Returns a "not allowed" view (naming the
        // category/subcategory) when the listing's subcategory has SponsorshipDisabled set, or
        // null when sponsorship is allowed. Sponsorship is allowed by default — an admin turns
        // it off per subcategory on the subcategory edit page for ones that are a potential issue.
        // Null when this listing may sponsor right now; otherwise the response to return.
        // Kill-switch first, then the eligibility rule (Verified + listed long enough), with the
        // grandfather clause for a CURRENTLY active sponsor.
        private async Task<IActionResult?> CannotSponsorAsync(DirectoryEntry entry)
        {
            var subcatBlocked = this.BlockedBySubcategorySponsorship(entry);
            if (subcatBlocked != null)
            {
                return subcatBlocked;
            }

            if (!this.IsEligibleToSponsor(entry)
                && !await this.sponsoredListingRepository.HasActiveSponsorshipAsync(entry.DirectoryEntryId).ConfigureAwait(false))
            {
                return this.BadRequest(new { Error = $"To sponsor, a listing must be Verified and listed in the directory for at least {DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring} days." });
            }

            return null;
        }

        private IActionResult? BlockedBySubcategorySponsorship(DirectoryEntry entry)
        {
            if (entry.SubCategory?.SponsorshipDisabled != true)
            {
                return null;
            }

            this.ViewBag.ListingName = entry.Name;
            this.ViewBag.ScopeLabel = FormattingHelper.SubcategoryFormatting(
                entry.SubCategory?.Category?.Name, entry.SubCategory?.Name);
            this.Response.StatusCode = StatusCodes.Status403Forbidden;
            return this.View("SponsorshipNotAllowed");
        }

        // Sponsorship eligibility: a listing must be Verified AND have been in the directory for
        // at least the required number of months. Both conditions are required.
        // Verified + listed long enough (current sponsors are allowed separately by each caller).
        private bool IsEligibleToSponsor(DirectoryEntry entry) =>
            SponsorshipEligibilityHelper.CanBuy(
                entry.DirectoryStatus,
                entry.CreateDate,
                isActiveSponsor: false,
                DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring,
                DateTime.UtcNow);

        private async Task<IEnumerable<DirectoryEntry>> FilterEntriesForSelectionAsync(int? subCategoryId, int? categoryId)
        {
            // Only surface listings that meet the sponsorship requirements (Verified + listed
            // long enough) OR are currently active sponsors (who may extend/re-sponsor), so the
            // public selection list never offers a truly ineligible entry.
            var activelySponsored = await this.sponsoredListingRepository.GetActivelySponsoredDirectoryEntryIdsAsync();
            var entries = (await this.directoryEntryRepository.GetAllowableAdvertisers())
                .Where(e => this.IsEligibleToSponsor(e) || activelySponsored.Contains(e.DirectoryEntryId));
            if (subCategoryId.HasValue)
            {
                entries = await this.InSubcategoryIncludingAdditionalAsync(entries.ToList(), subCategoryId.Value).ConfigureAwait(false);
            }
            else if (categoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory.CategoryId == categoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name).ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync())
                .OrderBy(sc => sc.Category.Name).ThenBy(sc => sc.Name).ToList();

            return entries;
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntriesByScopeAsync(SponsorshipType type, int typeId)
        {
            var activelySponsored = await this.sponsoredListingRepository.GetActivelySponsoredDirectoryEntryIdsAsync().ConfigureAwait(false);
            var entries = (await this.directoryEntryRepository.GetAllowableAdvertisers().ConfigureAwait(false))
                .Where(e => this.IsEligibleToSponsor(e) || activelySponsored.Contains(e.DirectoryEntryId));
            if (type == SponsorshipType.SubcategorySponsor)
            {
                entries = await this.InSubcategoryIncludingAdditionalAsync(entries.ToList(), typeId).ConfigureAwait(false);
            }
            else if (type == SponsorshipType.CategorySponsor)
            {
                entries = entries.Where(e => e.SubCategory != null && e.SubCategory.CategoryId == typeId).ToList();
            }

            return entries.OrderBy(e => e.Name).ToList();
        }

        // Listings in a subcategory for sponsorship: those whose main subcategory it is, plus those that
        // have it as an ADDITIONAL subcategory (returned as shown there, so the picker shows that name).
        private async Task<List<DirectoryEntry>> InSubcategoryIncludingAdditionalAsync(List<DirectoryEntry> eligible, int subCategoryId)
        {
            var result = eligible.Where(e => e.SubCategoryId == subCategoryId).ToList();
            if (this.additionalSubcategoryRepo == null)
            {
                return result;
            }

            foreach (var e in eligible.Where(e => e.SubCategoryId != subCategoryId))
            {
                var scoped = await this.ScopeEntryAsync(e, subCategoryId).ConfigureAwait(false);
                if (scoped != null && scoped.ShownViaAdditionalSubcategoryId.HasValue)
                {
                    result.Add(scoped);
                }
            }

            return result;
        }

        // A SUBCATEGORY sponsorship may be bought for one of the listing's additional subcategories.
        // Returns the listing as shown in that subcategory (SubCategory switched to it), so pricing,
        // slot counts, reservations, the kill-switch and the invoice all use that subcategory.
        // Anything else (no value, the main subcategory, a subcategory the listing isn't in) returns
        // the listing unchanged, so a buyer can never pick a subcategory the listing isn't listed in.
        private async Task<DirectoryEntry?> ScopeEntryAsync(DirectoryEntry? entry, int? scopeSubCategoryId)
        {
            if (entry == null || this.additionalSubcategoryRepo == null
                || !scopeSubCategoryId.HasValue || scopeSubCategoryId.Value <= 0
                || scopeSubCategoryId.Value == entry.SubCategoryId)
            {
                return entry;
            }

            var alt = (await this.additionalSubcategoryRepo.GetByDirectoryEntryIdAsync(entry.DirectoryEntryId).ConfigureAwait(false))
                .FirstOrDefault(a => a.SubCategoryId == scopeSubCategoryId.Value);
            return alt?.ApplyTo(entry) ?? entry;
        }

        // The additional subcategory a scoped entry is being sponsored in (null = main subcategory).
        private static int? ScopeOf(DirectoryEntry? entry) =>
            entry?.ShownViaAdditionalSubcategoryId.HasValue == true ? entry.SubCategoryId : null;

        // Loads the listing and the chosen offer. The additional-subcategory scope only applies to a
        // SUBCATEGORY offer; for main/category offers the plain listing (main subcategory) is used.
        private async Task<(DirectoryEntry? Entry, SponsoredListingOffer? Offer)> LoadEntryAndOfferAsync(
            int directoryEntryId, int selectedOfferId, int? scopeSubCategoryId)
        {
            var main = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (main == null)
            {
                return (null, null);
            }

            var scoped = await this.ScopeEntryAsync(main, scopeSubCategoryId).ConfigureAwait(false);
            var offer = await this.GetOfferForEntryAsync(selectedOfferId, scoped).ConfigureAwait(false);
            if (offer != null && offer.SponsorshipType != SponsorshipType.SubcategorySponsor)
            {
                return (main, offer);
            }

            return (scoped, offer);
        }

        // The offer id is the only thing a buyer submits, so it is checked against the price list
        // actually shown for this listing's placement (enabled, right tier, right category or
        // subcategory page). Returns null for an unknown, disabled or mismatched offer.
        private async Task<SponsoredListingOffer?> GetOfferForEntryAsync(int selectedOfferId, DirectoryEntry? entry)
        {
            if (entry is null || selectedOfferId <= 0)
            {
                return null;
            }

            // A listing scoped to an additional subcategory only changes the SUBCATEGORY placement;
            // the category placement is always the main subcategory's category.
            var main = entry.ShownViaAdditionalSubcategoryId.HasValue
                ? await this.directoryEntryRepository.GetByIdAsync(entry.DirectoryEntryId).ConfigureAwait(false) ?? entry
                : entry;

            foreach (var type in new[] { SponsorshipType.MainSponsor, SponsorshipType.CategorySponsor, SponsorshipType.SubcategorySponsor })
            {
                var placementEntry = type == SponsorshipType.SubcategorySponsor ? entry : main;
                var offers = await this.sponsoredListingOfferRepository
                    .GetOffersForPlacementAsync(type, PlacementIdFor(type, placementEntry)).ConfigureAwait(false);
                var match = offers.FirstOrDefault(o => o.SponsoredListingOfferId == selectedOfferId);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        // The placement a listing buys for a type: null for main, its category page for a category
        // sponsor, its (possibly additional) subcategory page for a subcategory sponsor.
        private static int? PlacementIdFor(SponsorshipType type, DirectoryEntry entry)
        {
            var id = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(type, entry, null, null);
            return id > 0 ? id : null;
        }

        private async Task<List<SponsoredListingOfferModel>> GetListingDurationsAsync(
            SponsorshipType type, int? placementId, decimal loyaltyDiscountPercent)
        {
            var offers = await this.sponsoredListingOfferRepository.GetOffersForPlacementAsync(type, placementId);
            return offers.OrderBy(x => x.Days).Select(o => new SponsoredListingOfferModel
            {
                SponsoredListingOfferId = o.SponsoredListingOfferId,
                Description = o.Description,
                Days = o.Days,
                USDPrice = o.Price,
                SponsorshipType = type,
                DiscountPercent = loyaltyDiscountPercent,
                DiscountedUSDPrice = ApplyPercentDiscount(o.Price, loyaltyDiscountPercent),
            }).ToList();
        }

        private async Task<SponsoredListingInvoice> CreateInvoiceAsync(
            DirectoryEntry entry, SponsoredListingOffer offer, DateTime startDate)
        {
            // The cross-tier existing-sponsor perk is recomputed server-side here so the charged amount is
            // authoritative — the buyer only ever submits an offer id, never a price.
            var (discountPercent, _) = await this.GetCrossTierDiscountAsync(entry.DirectoryEntryId, offer.SponsorshipType).ConfigureAwait(false);
            var netAmount = ApplyPercentDiscount(offer.Price, discountPercent);
            var hasDiscount = discountPercent > 0m;

            return await this.sponsoredListingInvoiceRepository.CreateAsync(new SponsoredListingInvoice
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                Currency = Currency.USD,
                InvoiceId = Guid.NewGuid(),
                PaymentStatus = PaymentStatus.InvoiceCreated,
                CampaignStartDate = startDate,
                CampaignEndDate = startDate.AddDays(offer.Days),
                Amount = netAmount,
                OriginalAmount = hasDiscount ? offer.Price : (decimal?)null,
                DiscountPercent = hasDiscount ? discountPercent : (decimal?)null,
                DiscountAmount = hasDiscount ? offer.Price - netAmount : (decimal?)null,
                InvoiceDescription = offer.Description,
                SponsorshipType = offer.SponsorshipType,
                SubCategoryId = entry.SubCategoryId,
                CategoryId = entry?.SubCategory?.CategoryId,
            });
        }

        // Cross-tier existing-sponsor perk lives in SponsorshipDiscountHelper so the checkout flow and the
        // sponsor options page share one source of truth (displayed price == charged price).
        private Task<(decimal Percent, SponsorshipType? BasisTier)> GetCrossTierDiscountAsync(
            int directoryEntryId, SponsorshipType targetType)
            => SponsorshipDiscountHelper.GetCrossTierDiscountAsync(this.sponsoredListingRepository, directoryEntryId, targetType);

        private static decimal ApplyPercentDiscount(decimal price, decimal percent)
            => SponsorshipDiscountHelper.ApplyPercentDiscount(price, percent);

        private async Task ApplyExistingSponsorPerkToOfferModelAsync(SponsoredListingOfferModel offerModel, int directoryEntryId)
        {
            var (percent, _) = await this.GetCrossTierDiscountAsync(directoryEntryId, offerModel.SponsorshipType).ConfigureAwait(false);
            offerModel.DiscountPercent = percent;
            offerModel.DiscountedUSDPrice = ApplyPercentDiscount(offerModel.USDPrice, percent);
        }

        private async Task<string> BuildReservationDetailsAsync(
            SponsorshipType type, DirectoryEntry entry, int? subCategoryId, int? categoryId)
        {
            var sb = new StringBuilder($"Type={type}; ListingId={entry.DirectoryEntryId}; ListingName=\"{entry.Name}\"");

            if (type == SponsorshipType.SubcategorySponsor)
            {
                int subId = subCategoryId ?? entry.SubCategoryId;
                string catName = "Unknown Category", subName = "Unknown Subcategory";
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

                sb.Append($"; SubcategoryId={subId}; Scope=\"{CategoryFormatter.Format(catName, subName)}\"");
            }
            else if (type == SponsorshipType.CategorySponsor)
            {
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

        private async Task<string> GetScopeLabelAsync(SponsorshipType type, int? typeId)
        {
            switch (type)
            {
                case SponsorshipType.MainSponsor: return "Main Sponsor";
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
                            return $"subcategory \"{CategoryFormatter.Format(cat?.Name ?? "Unknown", sub.Name)}\"";
                        }
                    }

                    return "the selected subcategory";
                default: return "this selection";
            }
        }

        private async Task<string> BuildCheckoutInProcessMessageAsync(SponsorshipType type, int? typeId, string group)
        {
            var scope = await this.GetScopeLabelAsync(type, typeId).ConfigureAwait(false);
            var max = SponsoredListingCheckoutHelper.GetMaxSlotsForType(type);
            var total = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(type, typeId).ConfigureAwait(false);

            if (total >= max)
            {
                var next = await this.GetNextOpeningUtcAsync(type, typeId).ConfigureAwait(false);
                return next.HasValue
                    ? $"No ad space available right now for {scope}. Next opening is expected around {next.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC."
                    : $"No ad space available right now for {scope}.";
            }

            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(group).ConfigureAwait(false);
            if (exp.HasValue)
            {
                return $"Another checkout for {scope} is in process and will expire at {exp.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC (in {Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes))} minutes).";
            }

            return $"Another checkout is currently in process for {scope}.";
        }

        private async Task<string> BuildMainSubcategoryLimitMessageAsync(int subCategoryId)
        {
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
            return $"No ad space available right now for Main Sponsor in {scope}. " +
                   $"This subcategory is limited to {Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory} Main Sponsors and it is full." +
                   (next.HasValue ? $" Next opening is expected around {next.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC." : string.Empty);
        }

        private async Task<string> BuildCheckoutInProcessMessageForMainSubcategoryAsync(int subCategoryId)
        {
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            var active = await this.GetActiveMainCountForSubcategoryAsync(subCategoryId).ConfigureAwait(false);

            if (active >= Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
            {
                var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
                return next.HasValue
                    ? $"No ad space available right now for Main Sponsor in {scope}. Next opening is expected around {next.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC."
                    : $"No ad space available right now for Main Sponsor in {scope}.";
            }

            var subGroup = ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(subCategoryId);
            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(subGroup).ConfigureAwait(false);
            if (exp.HasValue)
            {
                return $"Another checkout for Main Sponsor in {scope} is in process and will expire at {exp.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC (in {Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes))} minutes).";
            }

            return $"Another checkout is currently in process for Main Sponsor in {scope}.";
        }

        private async Task<DateTime?> GetNextOpeningUtcAsync(SponsorshipType type, int? typeId)
        {
            var all = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(type).ConfigureAwait(false);
            IEnumerable<SponsoredListing> scoped = type switch
            {
                SponsorshipType.MainSponsor => all,
                SponsorshipType.CategorySponsor => all.Where(x => x.CategoryId == (typeId ?? 0)),
                SponsorshipType.SubcategorySponsor => all.Where(x => x.SubCategoryId == typeId),
                _ => Enumerable.Empty<SponsoredListing>(),
            };
            return scoped.Any() ? scoped.Min(x => (DateTime?)x.CampaignEndDate) : null;
        }

        private async Task<DateTime?> GetNextOpeningUtcForMainSubcategoryAsync(int subCategoryId)
        {
            var utcNow = DateTime.UtcNow;
            var all = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor).ConfigureAwait(false);
            return all.Where(x => x.SubCategoryId == subCategoryId && x.CampaignEndDate > utcNow)
                      .OrderBy(x => x.CampaignEndDate)
                      .Select(x => (DateTime?)x.CampaignEndDate)
                      .FirstOrDefault();
        }

        private async Task<int> GetActiveMainCountForSubcategoryAsync(int subCategoryId)
        {
            var all = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor).ConfigureAwait(false);
            return all.Count(s =>
            {
                int subId = s.SubCategoryId.HasValue && s.SubCategoryId.Value > 0 ? s.SubCategoryId.Value : s.DirectoryEntry?.SubCategoryId ?? 0;
                return subId == subCategoryId;
            });
        }

        private async Task<bool> CanPurchaseMainWithinSubcategoryAsync(int subCategoryId) =>
            await this.GetActiveMainCountForSubcategoryAsync(subCategoryId).ConfigureAwait(false) < Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory;

        // =====================================================================
        // CONFIRM VALIDATION RESULT
        // =====================================================================
        private sealed class ConfirmValidationResult
        {
            public IActionResult? ErrorResult { get; private init; }
            public SponsoredListingOffer? Offer { get; private init; }
            public DirectoryEntry? Entry { get; private init; }
            public string? Link2Name { get; private init; }
            public string? Link3Name { get; private init; }
            public IEnumerable<SponsoredListing>? Current { get; private init; }

            public static ConfirmValidationResult Fail(IActionResult e) => new ConfirmValidationResult { ErrorResult = e };

            public static ConfirmValidationResult Ok(
                SponsoredListingOffer offer, DirectoryEntry entry, string l2, string l3, IEnumerable<SponsoredListing> current) =>
                new ConfirmValidationResult { Offer = offer, Entry = entry, Link2Name = l2, Link3Name = l3, Current = current };
        }
    }
}