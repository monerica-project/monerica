using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
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
using DirectoryManager.Web.Models.Sponsorship;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;
using System.Text;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingCheckoutController : BaseController
    {
        private const string ConfirmCheckoutView = "ConfirmCheckout";
        private const string CaptchaContextCheckout = "sponsoredlisting-confirmcheckout";

        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly INowPaymentsService paymentService;
        private readonly IBtcPayServerService btcPayServerService;
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISponsoredListingReservationRepository sponsoredListingReservationRepository;
        private readonly IBlockedIPRepository blockedIPRepository;
        private readonly ICacheService cacheService;
        private readonly IAffiliateAccountRepository affiliateRepo;
        private readonly IAffiliateCommissionRepository commissionRepo;
        private readonly ILogger<SponsoredListingCheckoutController> logger;

        public SponsoredListingCheckoutController(
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ITrafficLogRepository trafficLogRepository,
            INowPaymentsService paymentService,
            IBtcPayServerService btcPayServerService,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            IBlockedIPRepository blockedIPRepository,
            ICacheService cacheService,
            IAffiliateAccountRepository affiliateRepo,
            IAffiliateCommissionRepository commissionRepo,
            ILogger<SponsoredListingCheckoutController> logger)
            : base(trafficLogRepository, userAgentCacheService, cache)
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
            this.blockedIPRepository = blockedIPRepository;
            this.cacheService = cacheService;
            this.affiliateRepo = affiliateRepo;
            this.commissionRepo = commissionRepo;
            this.logger = logger;
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

            if (!this.IsOldEnough(entry))
            {
                return this.BadRequest(new { Error = $"Unverified listing must be listed for at least {IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising} days before advertising." });
            }

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(sponsorshipType, entry, subCategoryId, categoryId);
            var group = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);
            int? typeIdForCap = sponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, sponsorshipType).ConfigureAwait(false);
            var hasToken = await this.TryAttachReservationAsync(rsvId, group);

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
                var capacityError = await this.CheckListingCapacityAsync(sponsorshipType, typeIdForCap, group);
                if (capacityError != null)
                {
                    return this.BadRequest(new { Error = capacityError });
                }

                rsvId = await this.CreateReservationGuidAsync(sponsorshipType, entry, subCategoryId ?? entry.SubCategoryId, categoryId ?? entry.SubCategory?.CategoryId);
            }

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

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(sponsorshipType, entry, null, null);
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(sponsorshipType, typeIdForGroup);
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, sponsorshipType);

            if (rsvId.HasValue)
            {
                var existing = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);
                if (existing != null && existing.ReservationGroup == reservationGroup && existing.ExpirationDateTime > DateTime.UtcNow)
                {
                    this.ViewBag.ReservationGuid = rsvId;
                    this.ViewBag.ReservationExpiresUtc = existing.ExpirationDateTime;
                }
            }

            this.SetSponsorshipScopeViewBag(sponsorshipType, entry, typeIdForGroup);
            this.ViewBag.DirectoryEntrName = entry.Name;
            this.ViewBag.DirectoryEntryId = entry.DirectoryEntryId;
            this.ViewBag.SponsorshipType = sponsorshipType;
            this.ViewBag.RequiresReservationStart = !isExtension && this.ViewBag.ReservationGuid == null;

            return this.View(await this.GetListingDurationsAsync(sponsorshipType, entry.SubCategoryId));
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlisting/selectduration")]
        public async Task<IActionResult> SelectDurationAsync(int directoryEntryId, int selectedOfferId, Guid? rsvId = null)
        {
            var offer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId).ConfigureAwait(false);
            if (offer is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (entry is null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidListing });
            }

            var typeIdForGroup = SponsoredListingCheckoutHelper.ResolveTypeIdForGroup(offer.SponsorshipType, entry, null, null);
            var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeIdForGroup);
            int? typeIdForCap = offer.SponsorshipType == SponsorshipType.MainSponsor ? (int?)null : typeIdForGroup;
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType).ConfigureAwait(false);

            rsvId = await this.ValidateExistingReservationAsync(rsvId, reservationGroup);

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
                        return this.BadRequest(new { Error = rsvError });
                    }
                }

                var capacityError = await this.CheckListingCapacityAsync(offer.SponsorshipType, typeIdForCap, reservationGroup);
                if (capacityError != null)
                {
                    return this.BadRequest(new { Error = capacityError });
                }

                rsvId = await this.CreateReservationGuidAsync(offer.SponsorshipType, entry, entry.SubCategoryId, entry.SubCategory?.CategoryId);

                if (offer.SponsorshipType == SponsorshipType.MainSponsor)
                {
                    await this.CreateShadowSubcategoryReservationAsync(entry.SubCategoryId, entry, rsvId!.Value);
                }
            }

            return this.RedirectToAction("ConfirmCheckout", new { directoryEntryId, selectedOfferId, rsvId });
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
            string? referralCode = null)
        {
            var v = await this.ValidateConfirmRequestAsync(directoryEntryId, selectedOfferId, rsvId);
            if (v.ErrorResult != null)
            {
                return v.ErrorResult;
            }

            this.SetConfirmViewBag(v.Offer!, v.Entry!, referralCode);
            var vm = BuildConfirmationViewModel(v.Offer!, v.Entry!, v.Link2Name!, v.Link3Name!, v.Current!);
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
            string? referralCode = null)
        {
            var ipAddress = this.GetClientIpAddress();
            if (this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.NotFound();
            }

            if (!CaptchaTools.Validate(this.HttpContext, CaptchaContextCheckout, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                return await this.RebuildConfirmViewWithErrorAsync(directoryEntryId, selectedOfferId, rsvId, referralCode, email, "Captcha", "Incorrect CAPTCHA. Please try again.");
            }

            var offer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (offer == null || entry == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var (group, typeIdForCap) = this.BuildGroupAndCapacity(offer.SponsorshipType, entry);

            var reservationError = await this.HandleReservationForPostAsync(directoryEntryId, offer, rsvId, group, typeIdForCap);
            if (reservationError != null)
            {
                return reservationError;
            }

            var (emailOk, normalizedEmail, emailView) = await this.ValidateEmailAsync(email, directoryEntryId, selectedOfferId, rsvId, referralCode, offer, entry, group);
            if (!emailOk)
            {
                return emailView!;
            }

            var mainCapError = await this.CheckNewMainSponsorCapAsync(directoryEntryId, offer);
            if (mainCapError != null)
            {
                return this.BadRequest(new { Error = mainCapError });
            }

            var invoice = await this.BuildBaseInvoiceAsync(entry, offer, ipAddress, referralCode);

            if (string.Equals(selectedProcessor, "BTCPayServer", StringComparison.OrdinalIgnoreCase))
            {
                return await this.ExecuteBtcPayCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
            }

            if (string.Equals(selectedProcessor, "BTCPayServerNoJs", StringComparison.OrdinalIgnoreCase))
            {
                return await this.ExecuteBtcPayNoJsCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
            }

            return await this.ExecuteNowPaymentsCheckoutAsync(invoice, offer, rsvId, normalizedEmail);
        }

        // =====================================================================
        // NOWPAYMENTS IPN + SUCCESS
        // =====================================================================

        [HttpPost]
        [AllowAnonymous]
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

            if (SponsoredListingCheckoutHelper.IsTerminal(invoice.PaymentStatus))
            {
                if (invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    await this.TryCreateAffiliateCommissionAsync(invoice);
                }

                return this.Ok();
            }

            await this.ApplyIpnStatusUpdateAsync(invoice, msg);
            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/nowpaymentssuccess")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);
            if (processorInvoice?.OrderId == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            var invoice = await this.sponsoredListingInvoiceRepository.GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId)).ConfigureAwait(false);
            if (invoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            if (invoice.PaymentStatus == PaymentStatus.Canceled)
            {
                invoice.PaymentResponse = NP_id;
                await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
            }
            else if (invoice.PaymentStatus != PaymentStatus.Paid)
            {
                invoice.PaymentStatus = PaymentStatus.Paid;
                invoice.PaymentResponse = NP_id;
                await this.CreateNewSponsoredListingAsync(invoice);
                await this.TryCreateAffiliateCommissionAsync(invoice);
            }

            return this.View("NowPaymentsSuccess", new SuccessViewModel { OrderId = invoice.InvoiceId, ListingEndDate = invoice.CampaignEndDate });
        }

        // =====================================================================
        // BTCPAY WEBHOOK + SUCCESS
        // =====================================================================

        [HttpPost]
        [AllowAnonymous]
        [Route("sponsoredlisting/btcpaycallback")]
        public async Task<IActionResult> BtcPayCallbackAsync()
        {
            this.Request.EnableBuffering();
            var rawBody = await this.ReadBodyAsync(leaveOpen: true);
            this.logger.LogInformation("BTCPay webhook received: {Payload}", rawBody);

            var sig = this.Request.Headers["BTCPay-Sig"].FirstOrDefault() ?? string.Empty;
            if (!this.btcPayServerService.IsWebhookValid(rawBody, sig, out var sigError))
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
            if (invoice == null) { this.logger.LogWarning("BTCPay webhook: invoice not found for {Id}", payload.InvoiceId);
                return this.Ok(); }

            if (!SponsoredListingCheckoutHelper.IsTerminal(invoice.PaymentStatus))
            {
                await this.ApplyBtcPayWebhookUpdateAsync(invoice, payload);
            }

            return this.Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("sponsoredlisting/btcpaysuccess")]
        public async Task<IActionResult> BtcPaySuccess([FromQuery] Guid? orderId)
        {
            // orderId is our internal GUID embedded in the RedirectUrl we supplied to BTCPay.
            // Lookup by primary key — reliable regardless of redirect timing.
            if (!orderId.HasValue || orderId.Value == Guid.Empty)
            {
                this.logger.LogWarning("BtcPaySuccess reached without a valid orderId.");
                return this.View("BtcPaySuccess", new SuccessViewModel { OrderId = Guid.Empty, ListingEndDate = DateTime.MinValue });
            }

            var invoice = await this.sponsoredListingInvoiceRepository.GetByInvoiceIdAsync(orderId.Value).ConfigureAwait(false);
            if (invoice == null || invoice.PaymentProcessor != PaymentProcessor.BTCPayServer)
            {
                this.logger.LogWarning("BtcPaySuccess: no BTCPay invoice found for orderId {OrderId}", orderId);
                return this.View("BtcPaySuccess", new SuccessViewModel { OrderId = Guid.Empty, ListingEndDate = DateTime.MinValue });
            }

            // Verify payment with BTCPay using the DB-stored processor invoice ID.
            // The URL is never trusted for this — only the value we persisted at checkout.
            if (invoice.PaymentStatus != PaymentStatus.Paid && !string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId))
            {
                await this.TryConfirmBtcPayInvoiceAsync(invoice, invoice.ProcessorInvoiceId);
            }

            this.ViewBag.IsPending = invoice.PaymentStatus != PaymentStatus.Paid;
            return this.View("BtcPaySuccess", new SuccessViewModel { OrderId = invoice.InvoiceId, ListingEndDate = invoice.CampaignEndDate });
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

                    // If BTCPay says settled/complete but our DB hasn't caught up yet
                    // (webhook delay), update it now so the next refresh goes to success.
                    if (btcInvoice.IsSettled && dbInvoice.PaymentStatus != PaymentStatus.Paid)
                    {
                        await this.TryConfirmBtcPayInvoiceAsync(dbInvoice, processorInvoiceId)
                            .ConfigureAwait(false);

                        if (dbInvoice.PaymentStatus == PaymentStatus.Paid)
                        {
                            return this.RedirectToAction("BtcPaySuccess", new { orderId });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex,
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
                    vm.AmountDue = xmrMethod.Due is not (null or "0" or "0.0" or "")
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
                this.logger.LogWarning(ex,
                    "BtcPayNoJsInvoice: could not fetch XMR payment method for {Id}", processorInvoiceId);
            }

            return this.View("BtcPayNoJsInvoice", vm);
        }

        /// <summary>
        /// Returns a QR code PNG for the given BTCPay invoice's monero: payment URI.
        /// Looked up server-side from the processor invoice ID — no URI data in the URL.
        /// </summary>
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
                Amount = offer.Price.ToString("0.00"),
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
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false);

            return this.RedirectToAction("BtcPayNoJsInvoice", new
            {
                processorInvoiceId = btcPayInvoice.Id,
                orderId = invoice.InvoiceId,
            });
        }

        // =====================================================================
        // CHECKOUT ORCHESTRATION HELPERS
        // =====================================================================

        private async Task<ConfirmValidationResult> ValidateConfirmRequestAsync(int directoryEntryId, int selectedOfferId, Guid? rsvId)
        {
            var offer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId).ConfigureAwait(false);
            if (offer == null)
            {
                return ConfirmValidationResult.Fail(this.BadRequest(new { Error = StringConstants.InvalidOfferSelection }));
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId).ConfigureAwait(false);
            if (entry == null)
            {
                return ConfirmValidationResult.Fail(this.BadRequest(new { Error = StringConstants.InvalidSelection }));
            }

            var (group, typeIdForCap) = this.BuildGroupAndCapacity(offer.SponsorshipType, entry);
            var isExtension = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType).ConfigureAwait(false);

            if (!isExtension && offer.SponsorshipType == SponsorshipType.MainSponsor)
            {
                var capError = await this.CheckMainSubcategoryCapAsync(entry.SubCategoryId);
                if (capError != null)
                {
                    return ConfirmValidationResult.Fail(this.BadRequest(new { Error = capError }));
                }
            }

            if (!isExtension && !await this.TryAttachReservationAsync(rsvId, group))
            {
                var capacityError = await this.CheckListingCapacityAsync(offer.SponsorshipType, typeIdForCap, group);
                if (capacityError != null)
                {
                    return ConfirmValidationResult.Fail(this.BadRequest(new { Error = capacityError }));
                }
            }

            var l2 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var l3 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var current = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(offer.SponsorshipType).ConfigureAwait(false);
            return ConfirmValidationResult.Ok(offer, entry, l2, l3, current);
        }

        private void SetConfirmViewBag(SponsoredListingOffer offer, DirectoryEntry entry, string? referralCode)
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

            referralCode ??= this.Request.Query["ref"].ToString();
            this.ViewBag.ReferralCode = ReferralCodeHelper.NormalizeOrNull(referralCode) ?? string.Empty;
        }

        private async Task<IActionResult> RebuildConfirmViewWithErrorAsync(
            int directoryEntryId, int selectedOfferId, Guid? rsvId, string? referralCode,
            string? prefillEmail, string key, string message)
        {
            var offer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
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

            this.ViewBag.ReferralCode = ReferralCodeHelper.NormalizeOrNull(referralCode) ?? string.Empty;
            await this.TryAttachReservationAsync(rsvId, ReservationGroupHelper.BuildReservationGroupName(offer.SponsorshipType, typeId));

            var vm = BuildConfirmationViewModel(offer, entry, l2, l3, cur);
            vm.CanCreateSponsoredListing = true;
            this.ModelState.AddModelError(key, message);
            this.ViewBag.PrefillEmail = prefillEmail;
            return this.View(ConfirmCheckoutView, vm);
        }

        private async Task<IActionResult?> HandleReservationForPostAsync(
            int directoryEntryId, SponsoredListingOffer offer, Guid? rsvId, string group, int? typeIdForCap)
        {
            if (!await this.TryAttachReservationAsync(rsvId, group))
            {
                var isActive = await this.sponsoredListingRepository.IsSponsoredListingActive(directoryEntryId, offer.SponsorshipType);
                var totalActive = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(offer.SponsorshipType, typeIdForCap);
                var totalReserved = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(group);

                if (!SponsoredListingCheckoutHelper.CanPurchaseListing(totalActive, totalReserved, offer.SponsorshipType) && !isActive)
                {
                    return this.BadRequest(new { Error = await this.BuildCheckoutInProcessMessageAsync(offer.SponsorshipType, typeIdForCap, group) });
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
            if (existing == null || existing.PaymentStatus == PaymentStatus.Paid)
            {
                return;
            }

            existing.PaymentStatus = PaymentStatus.Canceled;
            existing.ReservationGuid = Guid.Empty;
            await this.sponsoredListingInvoiceRepository.UpdateAsync(existing);
        }

        private async Task<(bool ok, string? normalized, IActionResult? errorView)> ValidateEmailAsync(
            string? email, int directoryEntryId, int selectedOfferId, Guid? rsvId, string? referralCode,
            SponsoredListingOffer offer, DirectoryEntry entry, string group)
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

            return (false, null, await this.RebuildConfirmViewWithErrorAsync(directoryEntryId, selectedOfferId, rsvId, referralCode, email, "Email", emailError!));
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
            DirectoryEntry entry, SponsoredListingOffer offer, string ipAddress, string? referralCode)
        {
            var existing = await this.sponsoredListingRepository.GetActiveSponsorAsync(entry.DirectoryEntryId, offer.SponsorshipType);
            var startDate = existing?.CampaignEndDate ?? DateTime.UtcNow;
            var invoice = await this.CreateInvoiceAsync(entry, offer, startDate, ipAddress);
            if (ReferralCodeHelper.TryNormalize(referralCode, out var norm, out _) && !string.IsNullOrEmpty(norm))
            {
                invoice.ReferralCodeUsed = norm;
            }

            return invoice;
        }

        private async Task<IActionResult> ExecuteNowPaymentsCheckoutAsync(
            SponsoredListingInvoice invoice, SponsoredListingOffer offer, Guid? rsvId, string? normalizedEmail)
        {
            var req = new PaymentRequest
            {
                IsFeePaidByUser = false,
                PriceAmount = offer.Price,
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
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

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
                Amount = offer.Price.ToString("0.00"),
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
            try { btcPayInvoice = await this.btcPayServerService.CreateInvoiceAsync(req).ConfigureAwait(false); }
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
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
            return this.Redirect(btcPayInvoice.CheckoutLink);
        }

        // =====================================================================
        // NOWPAYMENTS IPN HELPERS
        // =====================================================================

        private IpnPaymentMessage? DeserializeIpnMessage(string payload)
        {
            try { return JsonConvert.DeserializeObject<IpnPaymentMessage>(payload); }
            catch (JsonException ex) { this.logger.LogWarning(ex, "NOWPayments IPN deserialize failed.");
                return null; }
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

            if (invoice == null) { this.logger.LogWarning("NOWPayments IPN: invoice not found. OrderId:{A} ProcessorId:{B}", msg.OrderId, ipnProcessorId);
                return null; }
            if (hasOrderGuid && invoice.InvoiceId != orderGuid) { this.logger.LogWarning("NOWPayments IPN: OrderId mismatch. IPN:{A} DB:{B}", msg.OrderId, invoice.InvoiceId); return null; }

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
            invoice.PaymentResponse = JsonConvert.SerializeObject(msg);
            invoice.PaidAmount = msg.PayAmount;
            invoice.OutcomeAmount = msg.OutcomeAmount;
            if (!string.IsNullOrWhiteSpace(msg.PayCurrency))
            {
                invoice.PaidInCurrency = EnumHelper.ParseStringToEnum<Currency>(msg.PayCurrency);
            }

            var proposed = SponsoredListingCheckoutHelper.ConvertToInternalStatus(
                EnumHelper.ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(msg.PaymentStatus));

            if (!SponsoredListingCheckoutHelper.IsTerminal(invoice.PaymentStatus))
            {
                var curRank = SponsoredListingCheckoutHelper.StatusRank[invoice.PaymentStatus];
                var nxtRank = SponsoredListingCheckoutHelper.StatusRank[proposed];
                if (nxtRank > curRank)
                {
                    invoice.PaymentStatus = proposed;
                }
                else if (nxtRank < curRank)
                {
                    this.logger.LogInformation("Ignoring status regression on invoice {Id}: {Cur} -> {Proposed}", invoice.InvoiceId, invoice.PaymentStatus, proposed);
                }
            }

            if (SponsoredListingCheckoutHelper.HoldExtendingStatuses.Contains(invoice.PaymentStatus))
            {
                await this.EnsureHoldFromInvoiceAsync(invoice, TimeSpan.FromHours(2), TimeSpan.FromHours(3));
            }

            await this.CreateNewSponsoredListingAsync(invoice);
            if (invoice.PaymentStatus == PaymentStatus.Paid)
            {
                await this.TryCreateAffiliateCommissionAsync(invoice);
            }
        }

        // =====================================================================
        // BTCPAY HELPERS
        // =====================================================================

        private async Task ApplyBtcPayWebhookUpdateAsync(SponsoredListingInvoice invoice, BtcPayWebhookPayload payload)
        {
            invoice.PaymentResponse = JsonConvert.SerializeObject(payload);

            var proposed = payload.Type switch
            {
                "InvoiceSettled" or "InvoicePaymentSettled" => PaymentStatus.Paid,
                "InvoiceProcessing" or "InvoiceReceivedPayment" => PaymentStatus.Pending,
                "InvoiceExpired" => PaymentStatus.Expired,
                "InvoiceInvalid" => PaymentStatus.Failed,
                _ => invoice.PaymentStatus,
            };

            if (!SponsoredListingCheckoutHelper.IsTerminal(invoice.PaymentStatus) &&
                SponsoredListingCheckoutHelper.StatusRank[proposed] > SponsoredListingCheckoutHelper.StatusRank[invoice.PaymentStatus])
            {
                invoice.PaymentStatus = proposed;
            }

            if (invoice.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Pending)
            {
                // InvoiceReceivedPayment and InvoicePaymentSettled carry payment.value directly
                // in the webhook body — use it immediately, no extra API call needed.
                // InvoiceSettled is an aggregate event with no per-payment detail, so fall back
                // to the payment-methods endpoint for that case.
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
                    // InvoiceSettled or webhook fired without payment detail — query the API.
                    await this.PopulateBtcPayPaymentAmountsAsync(invoice, payload.InvoiceId);
                }
            }

            if (SponsoredListingCheckoutHelper.HoldExtendingStatuses.Contains(invoice.PaymentStatus))
            {
                await this.EnsureHoldFromInvoiceAsync(invoice, TimeSpan.FromHours(2), TimeSpan.FromHours(3));
            }

            await this.CreateNewSponsoredListingAsync(invoice);
            if (invoice.PaymentStatus == PaymentStatus.Paid)
            {
                await this.TryCreateAffiliateCommissionAsync(invoice);
            }
        }

        private async Task TryConfirmBtcPayInvoiceAsync(SponsoredListingInvoice invoice, string processorInvoiceId)
        {
            try
            {
                var status = await this.btcPayServerService.GetInvoiceAsync(processorInvoiceId).ConfigureAwait(false);
                if (status.Status is "Settled" or "Complete")
                {
                    invoice.PaymentStatus = PaymentStatus.Paid;
                    invoice.PaymentResponse = JsonConvert.SerializeObject(status);
                    await this.PopulateBtcPayPaymentAmountsAsync(invoice, processorInvoiceId);
                    await this.CreateNewSponsoredListingAsync(invoice);
                    await this.TryCreateAffiliateCommissionAsync(invoice);
                }
            }
            catch (Exception ex) { this.logger.LogWarning(ex, "BTCPay status check failed for {InvoiceId}", processorInvoiceId); }
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
                invoice.PaidInCurrency = Currency.XMR;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not fetch payment amounts from BTCPay for invoice {Id}", processorInvoiceId);
            }
        }

        // =====================================================================
        // RESERVATION HELPERS
        // =====================================================================

        private async Task<Guid?> ValidateExistingReservationAsync(Guid? rsvId, string reservationGroup)
        {
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
            var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subCategoryId);
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

            var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subCategoryId);
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
                    Contact = de.Contact,
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
                IsExtension = current.Any(x => x.DirectoryEntryId == de.DirectoryEntryId),
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

        private string GetClientIpAddress()
        {
            var ip = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            return this.HttpContext.ShouldLogIp() ? ip : string.Empty;
        }

        private async Task<string> ReadBodyAsync(bool leaveOpen = false)
        {
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8, leaveOpen: leaveOpen);
            return await reader.ReadToEndAsync();
        }

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

            DateTime? expiresUtc = null;
            try { expiresUtc = (DateTime?)existing.GetType().GetProperty("ExpirationDate")?.GetValue(existing); } catch { }
            if (!expiresUtc.HasValue)
            {
                expiresUtc = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(reservationGroup).ConfigureAwait(false);
            }

            if (!expiresUtc.HasValue || expiresUtc.Value <= DateTime.UtcNow)
            {
                return false;
            }

            this.ViewBag.ReservationGuid = rsvId;
            this.ViewBag.ReservationExpiresUtc = expiresUtc.Value;
            return true;
        }

        private bool IsOldEnough(DirectoryEntry entry)
        {
            if (entry.CreateDate == DateTime.MinValue)
            {
                return false;
            }

            if (entry.DirectoryStatus == DirectoryStatus.Verified)
            {
                return true;
            }

            return (DateTime.UtcNow - entry.CreateDate).TotalDays >= IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising;
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntriesForSelectionAsync(int? subCategoryId, int? categoryId)
        {
            var entries = await this.directoryEntryRepository.GetAllowableAdvertisers();
            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategoryId == subCategoryId.Value).ToList();
            }
            else if (categoryId.HasValue) entries = entries.Where(e => e.SubCategory.CategoryId == categoryId.Value).ToList();
            entries = entries.OrderBy(e => e.Name).ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync())
                .OrderBy(sc => sc.Category.Name).ThenBy(sc => sc.Name).ToList();

            return entries;
        }

        private async Task<IEnumerable<DirectoryEntry>> FilterEntriesByScopeAsync(SponsorshipType type, int typeId)
        {
            var entries = await this.directoryEntryRepository.GetAllowableAdvertisers().ConfigureAwait(false);
            if (type == SponsorshipType.SubcategorySponsor) entries = entries.Where(e => e.SubCategoryId == typeId).ToList();
            else if (type == SponsorshipType.CategorySponsor) entries = entries.Where(e => e.SubCategory != null && e.SubCategory.CategoryId == typeId).ToList();
            return entries.OrderBy(e => e.Name).ToList();
        }

        private async Task<List<SponsoredListingOfferModel>> GetListingDurationsAsync(SponsorshipType type, int? subcategoryId)
        {
            var offers = await this.sponsoredListingOfferRepository.GetByTypeAndSubCategoryAsync(type, subcategoryId);
            return offers.OrderBy(x => x.Days).Select(o => new SponsoredListingOfferModel
            {
                SponsoredListingOfferId = o.SponsoredListingOfferId,
                Description = o.Description,
                Days = o.Days,
                USDPrice = o.Price,
            }).ToList();
        }

        private async Task<SponsoredListingInvoice> CreateInvoiceAsync(
            DirectoryEntry entry, SponsoredListingOffer offer, DateTime startDate, string ipAddress)
        {
            if (!this.HttpContext.ShouldLogIp())
            {
                ipAddress = string.Empty;
            }

            return await this.sponsoredListingInvoiceRepository.CreateAsync(new SponsoredListingInvoice
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                Currency = Currency.USD,
                InvoiceId = Guid.NewGuid(),
                PaymentStatus = PaymentStatus.InvoiceCreated,
                CampaignStartDate = startDate,
                CampaignEndDate = startDate.AddDays(offer.Days),
                Amount = offer.Price,
                InvoiceDescription = offer.Description,
                SponsorshipType = offer.SponsorshipType,
                SubCategoryId = entry.SubCategoryId,
                CategoryId = entry?.SubCategory?.CategoryId,
                IpAddress = ipAddress,
            });
        }

        private async Task CreateNewSponsoredListingAsync(SponsoredListingInvoice invoice)
        {
            if (!await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false)) return;
            if (invoice.PaymentStatus != PaymentStatus.Paid) return;

            if (await this.sponsoredListingRepository.GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId).ConfigureAwait(false) != null) return;

            var active = await this.sponsoredListingRepository.GetActiveSponsorAsync(invoice.DirectoryEntryId, invoice.SponsorshipType).ConfigureAwait(false);

            if (active == null)
            {
                var created = await this.sponsoredListingRepository.CreateAsync(new SponsoredListing
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

            var changed = false;
            if (invoice.CampaignEndDate > active.CampaignEndDate) { active.CampaignEndDate = invoice.CampaignEndDate; changed = true; }
            if (active.SponsoredListingInvoiceId != invoice.SponsoredListingInvoiceId) { active.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId; changed = true; }
            if (changed) await this.sponsoredListingRepository.UpdateAsync(active).ConfigureAwait(false);
            invoice.SponsoredListingId = active.SponsoredListingId;
            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice).ConfigureAwait(false);
            this.ClearCachedItems();
        }

        private async Task EnsureHoldFromInvoiceAsync(SponsoredListingInvoice invoice, TimeSpan min, TimeSpan max)
        {
            if (invoice.ReservationGuid == Guid.Empty || !SponsoredListingCheckoutHelper.HoldExtendingStatuses.Contains(invoice.PaymentStatus)) return;
            var target = DateTime.UtcNow.Add(min);
            var cap = DateTime.UtcNow.Add(max);
            await this.sponsoredListingReservationRepository.ExtendExpirationAsync(invoice.ReservationGuid, target > cap ? cap : target).ConfigureAwait(false);
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

                sb.Append($"; SubcategoryId={subId}; Scope=\"{catName} > {subName}\"");
            }
            else if (type == SponsorshipType.CategorySponsor)
            {
                int catId = categoryId ?? entry.SubCategory?.CategoryId ?? 0;
                string catName = "Unknown Category";
                if (catId > 0) { var cat = await this.categoryRepository.GetByIdAsync(catId).ConfigureAwait(false);
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
                    if (typeId.HasValue) { var cat = await this.categoryRepository.GetByIdAsync(typeId.Value).ConfigureAwait(false);
                        if (cat != null)
                        {
                            return $"category \"{cat.Name}\"";
                        }
                    }

                    return "the selected category";
                case SponsorshipType.SubcategorySponsor:
                    if (typeId.HasValue) { var sub = await this.subCategoryRepository.GetByIdAsync(typeId.Value).ConfigureAwait(false);
                        if (sub != null) { var cat = sub.Category ?? await this.categoryRepository.GetByIdAsync(sub.CategoryId).ConfigureAwait(false);
                            return $"subcategory \"{cat?.Name ?? "Unknown"} > {sub.Name}\""; } }
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
                    ? $"No ad space available right now for {scope}. Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC."
                    : $"No ad space available right now for {scope}.";
            }

            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(group).ConfigureAwait(false);
            if (exp.HasValue)
            {
                return $"Another checkout for {scope} is in process and will expire at {exp.Value:yyyy-MM-dd HH:mm} UTC (in {Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes))} minutes).";
            }

            return $"Another checkout is currently in process for {scope}.";
        }

        private async Task<string> BuildMainSubcategoryLimitMessageAsync(int subCategoryId)
        {
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
            return $"No ad space available right now for Main Sponsor in {scope}. " +
                   $"This subcategory is limited to {Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory} Main Sponsors and it is full." +
                   (next.HasValue ? $" Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC." : string.Empty);
        }

        private async Task<string> BuildCheckoutInProcessMessageForMainSubcategoryAsync(int subCategoryId)
        {
            var scope = await this.GetScopeLabelAsync(SponsorshipType.SubcategorySponsor, subCategoryId).ConfigureAwait(false);
            var active = await this.GetActiveMainCountForSubcategoryAsync(subCategoryId).ConfigureAwait(false);

            if (active >= Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
            {
                var next = await this.GetNextOpeningUtcForMainSubcategoryAsync(subCategoryId).ConfigureAwait(false);
                return next.HasValue
                    ? $"No ad space available right now for Main Sponsor in {scope}. Next opening is expected around {next.Value:yyyy-MM-dd HH:mm} UTC."
                    : $"No ad space available right now for Main Sponsor in {scope}.";
            }

            var subGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, subCategoryId);
            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(subGroup).ConfigureAwait(false);
            if (exp.HasValue)
            {
                return $"Another checkout for Main Sponsor in {scope} is in process and will expire at {exp.Value:yyyy-MM-dd HH:mm} UTC (in {Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes))} minutes).";
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

        private async Task TryCreateAffiliateCommissionAsync(SponsoredListingInvoice invoice, CancellationToken ct = default)
        {
            if (invoice?.PaymentStatus != PaymentStatus.Paid || string.IsNullOrWhiteSpace(invoice.ReferralCodeUsed))
            {
                return;
            }

            if (!ReferralCodeHelper.TryNormalize(invoice.ReferralCodeUsed, out var code, out _))
            {
                return;
            }

            var affiliate = await this.affiliateRepo.GetByReferralCodeAsync(code!, ct);
            if (affiliate == null || await this.commissionRepo.ExistsForInvoiceAsync(invoice.SponsoredListingInvoiceId, ct))
            {
                return;
            }

            if (await this.sponsoredListingInvoiceRepository.HasAnyPaidInvoiceForDirectoryEntryAsync(invoice.DirectoryEntryId, invoice.SponsoredListingInvoiceId, ct))
            {
                return;
            }

            await this.commissionRepo.AddAsync(
                new AffiliateCommission
            {
                SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                AffiliateAccountId = affiliate.AffiliateAccountId,
                AmountDue = Math.Round(invoice.OutcomeAmount * 0.50m, 8),
                PayoutCurrency = invoice.PaidInCurrency,
                PayoutStatus = CommissionPayoutStatus.Pending,
            }, ct);
        }

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