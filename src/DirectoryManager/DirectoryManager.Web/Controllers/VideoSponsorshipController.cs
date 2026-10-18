using BtcPayServer.API.Models;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.VideoSponsorships;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Sponsorship;
using DirectoryManager.Web.Models.VideoSponsorship;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Controllers
{
    // Public video-sponsorship pages and checkout. A listing owner picks their listing, a video
    // type (a segment inside upcoming videos, or a dedicated video), then pays in Monero through
    // the separate video-sponsorship BTCPay store. Entirely separate from the site-sponsor
    // checkout and its invoice table; only the low-level payment pieces are shared.
    [AllowAnonymous]
    [Route("video-sponsorship")]
    public class VideoSponsorshipController : BaseController
    {
        public const string CaptchaContext = "video-sponsorship-checkout";

        private const int SearchPageSize = 10;
        private const int MaxNotesLength = 4000;
        private const string DraftNotesKey = "VideoCheckoutDraftNotes";
        private const string DraftRefundAddressKey = "VideoCheckoutDraftRefundAddress";
        private const string DraftEmailKey = "VideoCheckoutDraftEmail";

        // The videos are locked for this long while the BTCPay invoice is being created; the
        // lock is then re-set to the invoice's real expiry.
        private static readonly TimeSpan InitialHold = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan HoldGraceAfterExpiry = TimeSpan.FromMinutes(2);

        private readonly IVideoSponsorshipRepository repository;
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IVideoSponsorPaymentGateway gateway;
        private readonly IVideoSponsorOrderProcessor orderProcessor;
        private readonly ICacheService cacheService;
        private readonly ILogger<VideoSponsorshipController> logger;

        public VideoSponsorshipController(
            IMemoryCache cache,
            IVideoSponsorshipRepository repository,
            IDirectoryEntryRepository entryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            IVideoSponsorPaymentGateway gateway,
            IVideoSponsorOrderProcessor orderProcessor,
            ICacheService cacheService,
            ILogger<VideoSponsorshipController> logger)
            : base(cache)
        {
            this.repository = repository;
            this.entryRepository = entryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.gateway = gateway;
            this.orderProcessor = orderProcessor;
            this.cacheService = cacheService;
            this.logger = logger;
        }

        // =====================================================================
        // PUBLIC PAGES
        // =====================================================================

        // Who sponsored which video, when, and for how much.
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var paidOrders = await this.repository.ListPaidOrdersAsync(ct);
            var vm = new VideoSponsorPublicVm
            {
                Offers = (await this.repository.GetOffersAsync(enabledOnly: true, ct)).Select(o => ToOfferRow(o, 0m)).ToList(),
                ChannelUrl = await this.GetChannelUrlAsync(),
                DeliveryDays = Common.Constants.IntegerConstants.VideoSponsorDeliveryDays,
                Sponsorships = paidOrders
                    .SelectMany(o => o.Items.Select(i => new VideoSponsorPublicRow
                    {
                        ListingName = o.DirectoryEntry?.Name ?? string.Empty,
                        DirectoryEntryKey = o.DirectoryEntry?.DirectoryEntryKey ?? string.Empty,
                        VideoTitle = i.VideoIdea?.Title ?? i.Title,
                        OfferName = o.OfferName,
                        PaidUtc = o.PaidUtc,
                        AmountUsd = i.Price,
                        VideoUrl = i.VideoUrl,
                    }))
                    .ToList(),
            };

            return this.View(vm);
        }

        // The queue of videos that are still open for a sponsor. A video that has
        // been paid for is no longer purchasable, so it drops off this list.
        [HttpGet("upcoming")]
        public async Task<IActionResult> Upcoming(CancellationToken ct)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var queued = await this.repository.GetIdeasAsync(VideoIdeaStatus.Queued, ct);

            var rows = queued
                .Where(v => v.SponsoredByOrderId == null && !v.IsDedicated)
                .Select(v => new VideoUpcomingRow
                {
                    Title = v.Title,
                    Description = v.Description,
                })
                .ToList();

            return this.View(rows);
        }

        // =====================================================================
        // CHECKOUT STEP 1: pick the listing
        // =====================================================================
        [HttpGet("start")]
        public async Task<IActionResult> Start(string? q, int page = 1)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            page = Math.Max(1, page);
            q = (q ?? string.Empty).Trim();

            var vm = new VideoSponsorStartVm
            {
                Query = q,
                Page = page,
                HasSearched = !string.IsNullOrWhiteSpace(q),
            };

            if (vm.HasSearched)
            {
                var result = await this.entryRepository.SearchAsync(q, page, SearchPageSize);
                var now = DateTime.UtcNow;

                vm.TotalCount = result.TotalCount;
                vm.TotalPages = (int)Math.Ceiling(result.TotalCount / (double)SearchPageSize);
                vm.Results = result.Items.Select(e =>
                {
                    var (can, reasons) = VideoSponsorEligibility.Check(e, now);
                    return new VideoSponsorListingRow
                    {
                        DirectoryEntryId = e.DirectoryEntryId,
                        Name = e.Name,
                        DirectoryEntryKey = e.DirectoryEntryKey,
                        Category = $"{e.SubCategory?.Category?.Name} > {e.SubCategory?.Name}",
                        CanSponsor = can,
                        Reasons = reasons,
                    };
                }).ToList();
            }

            return this.View(vm);
        }

        // =====================================================================
        // CHECKOUT STEP 2: pick the video type
        // =====================================================================
        [HttpGet("type/{directoryEntryId:int}")]
        public async Task<IActionResult> Type(int directoryEntryId, int? offerId = null, CancellationToken ct = default)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var (entry, entryError) = await this.LoadEligibleEntryAsync(directoryEntryId);
            if (entry is null)
            {
                return entryError!;
            }

            var vm = await this.BuildBaseVmAsync(entry);
            var offers = await this.repository.GetOffersAsync(enabledOnly: true, ct);
            vm.Offers = offers.Select(o => ToOfferRow(o, vm.DiscountPercent)).ToList();

            // Default to the per-video segment type (the first one that uses the video list).
            vm.SelectedOffer = vm.Offers.FirstOrDefault(o => o.VideoSponsorOfferId == offerId)
                ?? vm.Offers.FirstOrDefault(o => o.RequiresVideoSelection)
                ?? vm.Offers.FirstOrDefault();

            return this.View(vm);
        }

        [HttpPost("type")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TypePost(int directoryEntryId, int offerId, CancellationToken ct)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var offer = await this.repository.GetOfferAsync(offerId, ct);
            if (offer is null || !offer.IsEnabled)
            {
                return this.RedirectToAction(nameof(this.Type), new { directoryEntryId });
            }

            return offer.RequiresVideoSelection
                ? this.RedirectToAction(nameof(this.Videos), new { directoryEntryId, offerId })
                : this.RedirectToAction(nameof(this.Details), new { directoryEntryId, offerId });
        }

        // =====================================================================
        // CHECKOUT STEP 3: pick the videos (segment type only)
        // =====================================================================
        [HttpGet("videos")]
        public async Task<IActionResult> Videos(int directoryEntryId, int offerId, string? v = null, string? error = null, CancellationToken ct = default)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var (entry, entryError) = await this.LoadEligibleEntryAsync(directoryEntryId);
            if (entry is null)
            {
                return entryError!;
            }

            var offer = await this.repository.GetOfferAsync(offerId, ct);
            if (offer is null || !offer.IsEnabled || !offer.RequiresVideoSelection)
            {
                return this.RedirectToAction(nameof(this.Type), new { directoryEntryId });
            }

            var vm = await this.BuildBaseVmAsync(entry);
            vm.SelectedOffer = ToOfferRow(offer, vm.DiscountPercent);
            vm.AvailableVideos = await this.repository.GetAvailableIdeasAsync(DateTime.UtcNow, ct);
            vm.SelectedVideoIds = string.Join(",", ParseIds(v));
            vm.Error = error switch
            {
                "none" => "Select at least one video.",
                "taken" => "One of the videos you picked was just taken by another sponsor. Please choose again from the list below.",
                _ => null,
            };

            return this.View(vm);
        }

        [HttpPost("videos")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VideosPost(int directoryEntryId, int offerId, [FromForm] List<int>? videoIdeaIds)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var ids = (videoIdeaIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return this.RedirectToAction(nameof(this.Videos), new { directoryEntryId, offerId, error = "none" });
            }

            return this.RedirectToAction(nameof(this.Details), new { directoryEntryId, offerId, v = string.Join(",", ids) });
        }

        // =====================================================================
        // CHECKOUT STEP 4: notes, refund address, review the order
        // =====================================================================
        [HttpGet("details")]
        public async Task<IActionResult> Details(int directoryEntryId, int offerId, string? v = null, CancellationToken ct = default)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var (vm, redirect) = await this.BuildDetailsVmAsync(directoryEntryId, offerId, ParseIds(v), 0m, ct);
            if (vm is null)
            {
                return redirect!;
            }

            // Restore what the buyer typed before a selected video was taken (see Checkout).
            vm.Notes = this.TempData[DraftNotesKey] as string ?? vm.Notes;
            vm.RefundAddress = this.TempData[DraftRefundAddressKey] as string ?? vm.RefundAddress;
            vm.Email = this.TempData[DraftEmailKey] as string ?? vm.Email;
            return this.View(vm);
        }

        // =====================================================================
        // CHECKOUT STEP 5: create the order + Monero invoice
        // =====================================================================
        [HttpPost("checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(
            int directoryEntryId,
            int offerId,
            string? v,
            string? notes,
            string? refundAddress,
            string? email,
            bool demoOver100,
            string? demoCost,
            string? shownDemoCost,
            CancellationToken ct)
        {
            if (!await this.IsFeatureEnabledAsync())
            {
                return this.NotFound();
            }

            var ids = ParseIds(v);

            // Demo cost: more than $100 to demonstrate the product => the buyer pays it, in full.
            var demoOk = VideoSponsorPricing.TryResolveDemoCost(demoOver100, demoCost, out var demoCostUsd, out var demoError);

            var (vm, redirect) = await this.BuildDetailsVmAsync(directoryEntryId, offerId, ids, demoCostUsd, ct);
            if (vm is null)
            {
                return redirect!;
            }

            notes = (notes ?? string.Empty).Trim();
            refundAddress = (refundAddress ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim();
            vm.Notes = notes;
            vm.RefundAddress = refundAddress;
            vm.Email = email;
            vm.DemoCostInput = demoOver100 ? (demoCost ?? string.Empty).Trim() : null;

            if (!demoOk)
            {
                this.ModelState.AddModelError("DemoCost", demoError!);
                return this.View(nameof(this.Details), vm);
            }

            // The total on the page the buyer just submitted did not include this demo cost
            // (no JavaScript, so it can't update live). Show the new total and have them confirm
            // it before any invoice is created. Checked before the captcha so it isn't used up.
            _ = decimal.TryParse(shownDemoCost, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var shown);
            if (shown != demoCostUsd)
            {
                vm.Notice = demoCostUsd > 0m
                    ? "Your total now includes the demo cost. Check the order below, then confirm to continue to payment."
                    : "The demo cost was removed from your total. Check the order below, then confirm to continue to payment.";
                return this.View(nameof(this.Details), vm);
            }

            if (!CaptchaTools.Validate(this.HttpContext, CaptchaContext, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
            }

            if (!MoneroAddressValidator.IsValid(refundAddress))
            {
                this.ModelState.AddModelError("RefundAddress", "Enter a valid Monero address for refunds.");
            }

            if (email.Length > 0 && (email.Length > 100 || !ValidationHelper.IsValidEmail(email)))
            {
                this.ModelState.AddModelError("Email", "That email address doesn't look right. Leave it blank if you don't want to give one.");
            }

            if (notes.Length > MaxNotesLength)
            {
                this.ModelState.AddModelError("Notes", $"Notes can be at most {MaxNotesLength} characters.");
            }
            else if (!vm.SelectedOffer!.RequiresVideoSelection && notes.Length == 0)
            {
                this.ModelState.AddModelError("Notes", "Describe the video you want so we know what to make.");
            }

            var client = this.gateway.Client;
            if (client is null)
            {
                this.ModelState.AddModelError(string.Empty, "Checkout isn't available right now. Please try again later.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(nameof(this.Details), vm);
            }

            var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            var description = vm.SelectedOffer!.RequiresVideoSelection
                ? $"{siteName} video sponsorship: {vm.SelectedOffer.Name} x {vm.Quantity} for {vm.ListingName}"
                : $"{siteName} video sponsorship: {vm.SelectedOffer.Name} for {vm.ListingName}";
            if (vm.DemoCostUsd > 0m)
            {
                description += $" (includes ${vm.DemoCostUsd:0.00} product demo cost)";
            }

            var order = new VideoSponsorOrder
            {
                InvoiceId = Guid.NewGuid(),
                DirectoryEntryId = directoryEntryId,
                VideoSponsorOfferId = offerId,
                OfferName = vm.SelectedOffer.Name,
                UnitPrice = vm.SelectedOffer.Price,
                Quantity = vm.Quantity,
                OriginalAmount = vm.OriginalAmount,
                DiscountPercent = vm.DiscountPercent,
                DiscountAmount = vm.DiscountAmount,
                DiscountBasisTier = vm.DiscountPercent > 0m ? vm.DiscountBasisTier : null,
                DemoCostUsd = vm.DemoCostUsd,
                Amount = vm.Total,
                Currency = Currency.USD,
                PaymentStatus = PaymentStatus.InvoiceCreated,
                InvoiceDescription = description.Length > 500 ? description[..500] : description,
                Email = email.Length > 0 ? InputHelper.SetEmail(email) : null,
                Notes = notes.Length > 0 ? notes : null,
                RefundAddress = refundAddress,
            };

            var itemPrices = VideoSponsorPricing.SplitAcrossItems(vm.SponsorshipTotal, vm.Quantity);
            if (vm.SelectedOffer.RequiresVideoSelection)
            {
                for (var i = 0; i < vm.SelectedVideos.Count; i++)
                {
                    order.Items.Add(new VideoSponsorOrderItem
                    {
                        VideoIdeaId = vm.SelectedVideos[i].VideoIdeaId,
                        Title = vm.SelectedVideos[i].Title,
                        Price = itemPrices[i],
                    });
                }
            }
            else
            {
                order.Items.Add(new VideoSponsorOrderItem
                {
                    Title = $"Dedicated video: {vm.ListingName}",
                    Price = itemPrices[0],
                });
            }

            await this.repository.AddOrderAsync(order, ct);

            // Lock the chosen videos for this buyer. All-or-nothing.
            var now = DateTime.UtcNow;
            var held = await this.repository.TryHoldIdeasAsync(order.VideoSponsorOrderId, ids, now.Add(InitialHold), now, ct);
            if (!held)
            {
                order.PaymentStatus = PaymentStatus.Canceled;
                order.AdminNote = "Canceled at checkout: a selected video was no longer available.";
                await this.repository.UpdateOrderAsync(order, ct);

                // Keep the buyer's typed details for when they come back to this step.
                this.TempData[DraftNotesKey] = notes;
                this.TempData[DraftRefundAddressKey] = refundAddress;
                this.TempData[DraftEmailKey] = email;
                return this.RedirectToAction(nameof(this.Videos), new { directoryEntryId, offerId, error = "taken" });
            }

            var request = new BtcPayInvoiceRequest
            {
                Amount = order.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Currency = Currency.USD.ToString(),
                Metadata = new Dictionary<string, object>
                {
                    ["orderId"] = order.InvoiceId.ToString(),
                    ["itemDesc"] = order.InvoiceDescription,
                    ["videoSponsorOrderId"] = order.VideoSponsorOrderId,
                },
                Checkout = new BtcPayCheckoutOptions
                {
                    RedirectUrl = this.Url.Action(nameof(this.Success), "VideoSponsorship", new { orderId = order.InvoiceId }, this.Request.Scheme),
                    RedirectAutomatically = false,
                    DefaultPaymentMethod = Currency.XMR.ToString(),
                },
            };

            BtcPayInvoiceResponse processorInvoice;
            try
            {
                processorInvoice = await client!.CreateInvoiceAsync(request);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Video sponsorship: BTCPay invoice creation failed for order {OrderId}", order.InvoiceId);
                await this.repository.ReleaseHoldsAsync(order.VideoSponsorOrderId, ct);
                order.PaymentStatus = PaymentStatus.Failed;
                order.AdminNote = "Invoice could not be created at the payment processor.";
                await this.repository.UpdateOrderAsync(order, ct);

                this.ModelState.AddModelError(string.Empty, "We couldn't create your Monero invoice. Nothing was charged. Please try again in a few minutes.");
                return this.View(nameof(this.Details), vm);
            }

            order.ProcessorInvoiceId = processorInvoice.Id;
            order.InvoiceRequest = JsonConvert.SerializeObject(request);
            order.InvoiceResponse = JsonConvert.SerializeObject(processorInvoice);
            order.PaidInCurrency = Currency.XMR;
            order.InvoiceExpiresUtc = processorInvoice.ExpirationTime > 0 ? processorInvoice.ExpirationTimeUtc : now.Add(InitialHold);
            await this.repository.UpdateOrderAsync(order, ct);

            // Keep the videos locked for exactly as long as the invoice can still be paid.
            await this.repository.SetHoldExpiryAsync(
                order.VideoSponsorOrderId, order.InvoiceExpiresUtc.Value.Add(HoldGraceAfterExpiry), ct);

            return this.RedirectToAction(nameof(this.Invoice), new { orderId = order.InvoiceId });
        }

        // =====================================================================
        // INVOICE / PAYMENT PAGES (no JavaScript)
        // =====================================================================
        [HttpGet("invoice/{orderId:guid}")]
        public async Task<IActionResult> Invoice(Guid orderId, CancellationToken ct)
        {
            var order = await this.repository.GetOrderByInvoiceIdAsync(orderId, ct);
            if (order is null || string.IsNullOrWhiteSpace(order.ProcessorInvoiceId))
            {
                return this.NotFound();
            }

            // The webhook is authoritative, but check BTCPay here too so the buyer is never
            // left waiting on a delayed or missed webhook.
            await this.orderProcessor.SyncFromProcessorAsync(order, ct);

            if (SponsoredListingCheckoutHelper.IsPaidOrOverpaid(order.PaymentStatus)
                || order.PaymentStatus == PaymentStatus.UnderPayment)
            {
                return this.RedirectToAction(nameof(this.Success), new { orderId });
            }

            var vm = new BtcPayNoJsInvoiceViewModel
            {
                OrderId = orderId,
                ProcessorInvoiceId = order.ProcessorInvoiceId,
                InvoiceDescription = order.InvoiceDescription,
                ListingName = order.DirectoryEntry?.Name ?? string.Empty,
                Status = order.PaymentStatus is PaymentStatus.Expired or PaymentStatus.Failed or PaymentStatus.Canceled ? "Expired" : "New",
                ExpiresAt = order.InvoiceExpiresUtc,
                QrUrlOverride = this.Url.Action(nameof(this.Qr), new { orderId }),
                RefreshUrlOverride = this.Url.Action(nameof(this.Invoice), new { orderId }),
                SuccessUrlOverride = this.Url.Action(nameof(this.Success), new { orderId }),
                StartOverUrlOverride = this.Url.Action(nameof(this.Start)),
            };

            var client = this.gateway.Client;
            if (client is not null && !vm.IsExpired)
            {
                try
                {
                    var processorInvoice = await client.GetInvoiceAsync(order.ProcessorInvoiceId);
                    vm.Status = processorInvoice.Status ?? "New";
                    if (processorInvoice.ExpirationTime > 0)
                    {
                        vm.ExpiresAt = processorInvoice.ExpirationTimeUtc;
                    }

                    var xmr = await client.GetXmrPaymentMethodAsync(order.ProcessorInvoiceId);
                    if (xmr is not null)
                    {
                        vm.Address = xmr.Destination ?? string.Empty;
                        vm.AmountDue = xmr.Due is not (null or "0" or "0.0" or "")
                            ? xmr.Due
                            : xmr.Amount ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(vm.Address) && !string.IsNullOrWhiteSpace(vm.AmountDue))
                        {
                            vm.PaymentUri = $"monero:{vm.Address}?tx_amount={vm.AmountDue}";
                        }

                        if (xmr.Payments?.Count > 0)
                        {
                            var rawId = xmr.Payments[0].Id ?? string.Empty;
                            vm.TxId = rawId.Contains('#') ? rawId.Split('#')[0] : rawId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Page still renders (with a refresh link) if BTCPay is momentarily unreachable.
                    this.logger.LogWarning(ex, "Video sponsorship: could not load BTCPay invoice {Id}", order.ProcessorInvoiceId);
                }
            }

            return this.View("~/Views/SponsoredListingCheckout/BtcPayNoJsInvoice.cshtml", vm);
        }

        [HttpGet("qr/{orderId:guid}")]
        public async Task<IActionResult> Qr(Guid orderId, CancellationToken ct)
        {
            var order = await this.repository.GetOrderByInvoiceIdAsync(orderId, ct);
            var client = this.gateway.Client;
            if (order is null || client is null || string.IsNullOrWhiteSpace(order.ProcessorInvoiceId))
            {
                return this.NotFound();
            }

            string paymentUri;
            try
            {
                var xmr = await client.GetXmrPaymentMethodAsync(order.ProcessorInvoiceId);
                if (xmr is null || string.IsNullOrWhiteSpace(xmr.Destination))
                {
                    return this.NotFound();
                }

                var amount = xmr.Due ?? xmr.Amount ?? string.Empty;
                paymentUri = string.IsNullOrWhiteSpace(amount)
                    ? $"monero:{xmr.Destination}"
                    : $"monero:{xmr.Destination}?tx_amount={amount}";
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Video sponsorship: could not build QR for {Id}", order.ProcessorInvoiceId);
                return this.NotFound();
            }

            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(paymentUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var png = new QRCoder.PngByteQRCode(data);
            return this.File(png.GetGraphic(6), "image/png");
        }

        [HttpGet("success/{orderId:guid}")]
        public async Task<IActionResult> Success(Guid orderId, CancellationToken ct)
        {
            var order = await this.repository.GetOrderByInvoiceIdAsync(orderId, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            await this.orderProcessor.SyncFromProcessorAsync(order, ct);

            return this.View(new VideoSponsorSuccessVm
            {
                Order = order,
                IsPaid = SponsoredListingCheckoutHelper.IsPaidOrOverpaid(order.PaymentStatus),
                IsUnderPaid = order.PaymentStatus == PaymentStatus.UnderPayment,
                DeliveryDays = Common.Constants.IntegerConstants.VideoSponsorDeliveryDays,
            });
        }

        // =====================================================================
        // BTCPAY WEBHOOK (video-sponsorship store)
        // =====================================================================
        [HttpPost("btcpay-callback")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BtcPayCallback(CancellationToken ct)
        {
            var client = this.gateway.Client;
            if (client is null)
            {
                return this.Ok();
            }

            string rawBody;
            using (var reader = new StreamReader(this.Request.Body, System.Text.Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync(ct);
            }

            var signature = this.Request.Headers["BTCPay-Sig"].FirstOrDefault() ?? string.Empty;
            if (!client.IsWebhookValid(rawBody, signature, out var signatureError))
            {
                this.logger.LogWarning("Video sponsorship webhook signature invalid: {Error}", signatureError);
                return this.Ok();
            }

            BtcPayWebhookPayload? payload;
            try
            {
                payload = JsonConvert.DeserializeObject<BtcPayWebhookPayload>(rawBody);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "Video sponsorship webhook could not be read.");
                return this.Ok();
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.InvoiceId))
            {
                return this.Ok();
            }

            var order = await this.repository.GetOrderByProcessorInvoiceIdAsync(payload.InvoiceId, ct);
            if (order is null)
            {
                this.logger.LogWarning("Video sponsorship webhook: no order for processor invoice {Id}", payload.InvoiceId);
                return this.Ok();
            }

            var proposed = SponsoredListingCheckoutHelper.MapBtcPayEventToInternalStatus(
                payload.Type, partiallyPaid: payload.PartiallyPaid);

            await this.orderProcessor.ApplyStatusAsync(order, proposed, JsonConvert.SerializeObject(payload), ct: ct);
            return this.Ok();
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static List<int> ParseIds(string? csv) =>
            (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Take(50)
                .ToList();

        private static VideoSponsorOfferRow ToOfferRow(VideoSponsorOffer offer, decimal discountPercent) => new ()
        {
            VideoSponsorOfferId = offer.VideoSponsorOfferId,
            Name = offer.Name,
            Description = offer.Description,
            RequiresVideoSelection = offer.RequiresVideoSelection,
            Price = offer.PriceUsd,
            DiscountedPrice = SponsorshipDiscountHelper.ApplyPercentDiscount(offer.PriceUsd, discountPercent),
        };

        private async Task<bool> IsFeatureEnabledAsync()
        {
            var value = await this.cacheService.GetSnippetAsync(SiteConfigSetting.VideoSponsorshipEnabled);
            return string.Equals((value ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> GetChannelUrlAsync() =>
            ((await this.cacheService.GetSnippetAsync(SiteConfigSetting.VideoSponsorshipChannelUrl)) ?? string.Empty).Trim();

        // The listing must exist and pass the video-sponsor rules; otherwise back to the search,
        // which explains what is missing.
        private async Task<(DirectoryEntry? Entry, IActionResult? Error)> LoadEligibleEntryAsync(int directoryEntryId)
        {
            var entry = await this.entryRepository.GetByIdAsync(directoryEntryId);
            if (entry is null)
            {
                return (null, this.NotFound());
            }

            var (canSponsor, _) = VideoSponsorEligibility.Check(entry, DateTime.UtcNow);
            if (!canSponsor)
            {
                return (null, this.RedirectToAction(nameof(this.Start), new { q = entry.Name }));
            }

            return (entry, null);
        }

        private async Task<VideoSponsorCheckoutVm> BuildBaseVmAsync(DirectoryEntry entry)
        {
            var (percent, basisTier) = await SponsorshipDiscountHelper.GetHighestHeldTierPerkAsync(
                this.sponsoredListingRepository, entry.DirectoryEntryId);

            return new VideoSponsorCheckoutVm
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                ListingName = entry.Name,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DiscountPercent = percent,
                DiscountBasisTier = basisTier,
                ChannelUrl = await this.GetChannelUrlAsync(),
                CheckoutAvailable = this.gateway.IsConfigured,
                DeliveryDays = Common.Constants.IntegerConstants.VideoSponsorDeliveryDays,
            };
        }

        // Re-validates the whole selection (listing, type, videos) and prices it. Used to show the
        // details page and again, authoritatively, when the order is created.
        private async Task<(VideoSponsorCheckoutVm? Vm, IActionResult? Redirect)> BuildDetailsVmAsync(
            int directoryEntryId, int offerId, List<int> videoIdeaIds, decimal demoCostUsd, CancellationToken ct)
        {
            var (entry, entryError) = await this.LoadEligibleEntryAsync(directoryEntryId);
            if (entry is null)
            {
                return (null, entryError);
            }

            var offer = await this.repository.GetOfferAsync(offerId, ct);
            if (offer is null || !offer.IsEnabled)
            {
                return (null, this.RedirectToAction(nameof(this.Type), new { directoryEntryId }));
            }

            var vm = await this.BuildBaseVmAsync(entry);
            vm.SelectedOffer = ToOfferRow(offer, vm.DiscountPercent);

            if (offer.RequiresVideoSelection)
            {
                if (videoIdeaIds.Count == 0)
                {
                    return (null, this.RedirectToAction(nameof(this.Videos), new { directoryEntryId, offerId, error = "none" }));
                }

                var available = await this.repository.GetAvailableIdeasAsync(DateTime.UtcNow, ct);
                vm.SelectedVideos = available.Where(a => videoIdeaIds.Contains(a.VideoIdeaId)).ToList();
                if (vm.SelectedVideos.Count != videoIdeaIds.Count)
                {
                    return (null, this.RedirectToAction(nameof(this.Videos), new { directoryEntryId, offerId, error = "taken" }));
                }

                vm.Quantity = vm.SelectedVideos.Count;
                vm.SelectedVideoIds = string.Join(",", vm.SelectedVideos.Select(s => s.VideoIdeaId));
            }
            else
            {
                vm.Quantity = 1;
            }

            var (original, discountAmount, total) = VideoSponsorPricing.Compute(offer.PriceUsd, vm.Quantity, vm.DiscountPercent);
            vm.OriginalAmount = original;
            vm.DiscountAmount = discountAmount;
            vm.SponsorshipTotal = total;
            vm.DemoCostUsd = demoCostUsd;
            vm.Total = total + demoCostUsd;
            return (vm, null);
        }
    }
}
