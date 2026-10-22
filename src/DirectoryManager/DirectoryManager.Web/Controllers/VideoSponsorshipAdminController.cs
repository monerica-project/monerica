using System.Globalization;
using System.Net;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.VideoSponsorships;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.VideoSponsorship;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Controllers
{
    // Admin side of video sponsorships: settings (on/off), video types + prices, the queue of
    // upcoming videos, orders (video URL + one-time "it's live" email), the sponsors list and
    // revenue reporting. Kept separate from the site-sponsor invoicing and reports.
    [Authorize]
    [Route("admin/video-sponsorship")]
    public class VideoSponsorshipAdminController : BaseController
    {
        private readonly IVideoSponsorshipRepository repository;
        private readonly IContentSnippetRepository contentSnippetRepository;
        private readonly IVideoSponsorPaymentGateway gateway;
        private readonly IVideoSponsorOrderProcessor orderProcessor;
        private readonly ICacheService cacheService;

        public VideoSponsorshipAdminController(
            IMemoryCache cache,
            IVideoSponsorshipRepository repository,
            IContentSnippetRepository contentSnippetRepository,
            IVideoSponsorPaymentGateway gateway,
            IVideoSponsorOrderProcessor orderProcessor,
            ICacheService cacheService)
            : base(cache)
        {
            this.repository = repository;
            this.contentSnippetRepository = contentSnippetRepository;
            this.gateway = gateway;
            this.orderProcessor = orderProcessor;
            this.cacheService = cacheService;
        }

        // =====================================================================
        // HOME + SETTINGS
        // =====================================================================
        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var queued = await this.repository.GetIdeasAsync(VideoIdeaStatus.Queued, ct);
            var orders = await this.repository.ListOrdersAsync(ct);
            var paid = orders.Where(o => SponsoredListingCheckoutHelper.IsPaidOrOverpaid(o.PaymentStatus)).ToList();

            return this.View(new VideoSponsorAdminHomeVm
            {
                IsEnabled = IsTrue(this.contentSnippetRepository.GetValue(SiteConfigSetting.VideoSponsorshipEnabled)),
                IsPaymentConfigured = this.gateway.IsConfigured,
                ChannelUrl = (this.contentSnippetRepository.GetValue(SiteConfigSetting.VideoSponsorshipChannelUrl) ?? string.Empty).Trim(),
                VideoSiteUrl = (this.contentSnippetRepository.GetValue(SiteConfigSetting.VideoSponsorshipVideoSiteUrl) ?? string.Empty).Trim(),
                WebhookUrl = this.Url.Action("BtcPayCallback", "VideoSponsorship", null, this.Request.Scheme) ?? string.Empty,
                QueuedVideos = queued.Count,
                OpenToSponsor = (await this.repository.GetAvailableIdeasAsync(now, ct)).Count,
                PaidOrders = paid.Count,
                VideosOutstanding = paid.SelectMany(o => o.Items).Count(i => i.CompletedUtc is null),
                OrdersNeedingAttention = orders.Count(NeedsAttention),
            });
        }

        [HttpPost("settings")]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSettings(bool isEnabled, string? channelUrl, string? videoSiteUrl)
        {
            channelUrl = (channelUrl ?? string.Empty).Trim();
            videoSiteUrl = (videoSiteUrl ?? string.Empty).Trim();
            if ((channelUrl.Length > 0 && !IsHttpUrl(channelUrl)) || (videoSiteUrl.Length > 0 && !IsHttpUrl(videoSiteUrl)))
            {
                this.TempData[StringConstants.ErrorMessage] = "The channel and video site URLs must start with http:// or https://.";
                return this.RedirectToAction(nameof(this.Index));
            }

            this.SaveSnippet(SiteConfigSetting.VideoSponsorshipEnabled, isEnabled ? "true" : "false");
            this.SaveSnippet(SiteConfigSetting.VideoSponsorshipChannelUrl, channelUrl);
            this.SaveSnippet(SiteConfigSetting.VideoSponsorshipVideoSiteUrl, videoSiteUrl);

            this.TempData[StringConstants.SuccessMessage] = isEnabled
                ? "Saved. Video sponsorship is ON."
                : "Saved. Video sponsorship is OFF (public pages return Not Found).";
            return this.RedirectToAction(nameof(this.Index));
        }

        // =====================================================================
        // VIDEO TYPES + PRICES
        // =====================================================================
        [HttpGet("offers")]
        public async Task<IActionResult> Offers(CancellationToken ct) =>
            this.View(await this.repository.GetOffersAsync(enabledOnly: false, ct));

        [HttpGet("offers/edit")]
        public async Task<IActionResult> EditOffer(int? id, CancellationToken ct)
        {
            if (id is null)
            {
                return this.View(new VideoSponsorOffer { IsEnabled = true, RequiresVideoSelection = true });
            }

            var offer = await this.repository.GetOfferAsync(id.Value, ct);
            return offer is null ? this.NotFound() : this.View(offer);
        }

        [HttpPost("offers/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOffer(
            int videoSponsorOfferId,
            string? name,
            string? description,
            decimal priceUsd,
            bool requiresVideoSelection,
            bool isEnabled,
            int sortOrder,
            CancellationToken ct)
        {
            name = (name ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();

            if (name.Length == 0 || name.Length > 150 || description.Length > 1000 || priceUsd <= 0m || priceUsd > 1_000_000m)
            {
                this.TempData[StringConstants.ErrorMessage] = "Enter a name (up to 150 characters) and a price above zero.";
                return this.RedirectToAction(nameof(this.EditOffer), new { id = videoSponsorOfferId == 0 ? (int?)null : videoSponsorOfferId });
            }

            var offer = videoSponsorOfferId == 0
                ? new VideoSponsorOffer()
                : await this.repository.GetOfferAsync(videoSponsorOfferId, ct);
            if (offer is null)
            {
                return this.NotFound();
            }

            offer.Name = name;
            offer.Description = description.Length > 0 ? description : null;
            offer.PriceUsd = decimal.Round(priceUsd, 2);
            offer.RequiresVideoSelection = requiresVideoSelection;
            offer.IsEnabled = isEnabled;
            offer.SortOrder = sortOrder;

            if (videoSponsorOfferId == 0)
            {
                await this.repository.AddOfferAsync(offer, ct);
            }
            else
            {
                await this.repository.UpdateOfferAsync(offer, ct);
            }

            this.TempData[StringConstants.SuccessMessage] = "Video type saved.";
            return this.RedirectToAction(nameof(this.Offers));
        }

        // =====================================================================
        // VIDEO QUEUE
        // =====================================================================
        [HttpGet("videos")]
        public async Task<IActionResult> Videos(CancellationToken ct)
        {
            var orders = await this.repository.ListOrdersAsync(ct);
            this.ViewBag.OrdersById = orders.ToDictionary(o => o.VideoSponsorOrderId);
            return this.View(await this.repository.GetIdeasAsync(null, ct));
        }

        [HttpGet("videos/edit")]
        public async Task<IActionResult> EditVideo(int? id, CancellationToken ct)
        {
            if (id is null)
            {
                return this.View(new VideoIdea { Status = VideoIdeaStatus.Queued });
            }

            var idea = await this.repository.GetIdeaAsync(id.Value, ct);
            return idea is null ? this.NotFound() : this.View(idea);
        }

        [HttpPost("videos/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVideo(
            int videoIdeaId,
            string? title,
            string? description,
            VideoIdeaStatus status,
            int sortOrder,
            string? videoUrl,
            CancellationToken ct)
        {
            title = (title ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();
            videoUrl = (videoUrl ?? string.Empty).Trim();
            var editRoute = new { id = videoIdeaId == 0 ? (int?)null : videoIdeaId };

            if (title.Length == 0 || title.Length > 200 || description.Length > 2000)
            {
                this.TempData[StringConstants.ErrorMessage] = "Enter a topic (up to 200 characters). The description can be up to 2000.";
                return this.RedirectToAction(nameof(this.EditVideo), editRoute);
            }

            if (status is not (VideoIdeaStatus.Queued or VideoIdeaStatus.Completed or VideoIdeaStatus.Retired))
            {
                status = VideoIdeaStatus.Queued;
            }

            if (videoUrl.Length > 0 && (videoUrl.Length > 500 || !IsHttpUrl(videoUrl)))
            {
                this.TempData[StringConstants.ErrorMessage] = "The video URL must start with http:// or https://.";
                return this.RedirectToAction(nameof(this.EditVideo), editRoute);
            }

            var idea = videoIdeaId == 0 ? new VideoIdea() : await this.repository.GetIdeaAsync(videoIdeaId, ct);
            if (idea is null)
            {
                return this.NotFound();
            }

            // A video someone has paid for can't be dropped from the queue by accident.
            if (status == VideoIdeaStatus.Retired && idea.SponsoredByOrderId is not null)
            {
                this.TempData[StringConstants.ErrorMessage] = "This video has a paying sponsor, so it can't be retired. Refund and cancel the order first.";
                return this.RedirectToAction(nameof(this.EditVideo), editRoute);
            }

            // Each topic appears once in the list (dedicated videos are named after their sponsor and are exempt).
            if (!idea.IsDedicated && await this.repository.IdeaTopicExistsAsync(title, videoIdeaId, ct))
            {
                this.TempData[StringConstants.ErrorMessage] = $"A video idea with the topic \"{title}\" already exists. Each topic can only be in the list once.";
                return this.RedirectToAction(nameof(this.EditVideo), editRoute);
            }

            idea.Title = title;
            idea.Description = description.Length > 0 ? description : null;
            idea.Status = status;
            idea.SortOrder = sortOrder;
            idea.VideoUrl = videoUrl.Length > 0 ? videoUrl : null;

            try
            {
                if (videoIdeaId == 0)
                {
                    await this.repository.AddIdeaAsync(idea, ct);
                }
                else
                {
                    await this.repository.UpdateIdeaAsync(idea, ct);
                }
            }
            catch (DbUpdateException)
            {
                // The unique index caught a duplicate topic saved at the same moment.
                this.TempData[StringConstants.ErrorMessage] = $"A video idea with the topic \"{title}\" already exists. Each topic can only be in the list once.";
                return this.RedirectToAction(nameof(this.EditVideo), editRoute);
            }

            this.TempData[StringConstants.SuccessMessage] = "Video idea saved.";
            return this.RedirectToAction(nameof(this.Videos));
        }

        // =====================================================================
        // ORDERS
        // =====================================================================
        [HttpGet("orders")]
        public async Task<IActionResult> Orders(string filter = "paid", CancellationToken ct = default)
        {
            var orders = await this.repository.ListOrdersAsync(ct);
            filter = (filter ?? "paid").Trim().ToLowerInvariant();

            var shown = filter switch
            {
                "all" => orders,
                "attention" => orders.Where(NeedsAttention).ToList(),
                "open" => orders.Where(o => o.PaymentStatus is PaymentStatus.InvoiceCreated or PaymentStatus.Pending).ToList(),
                "outstanding" => orders.Where(o => SponsoredListingCheckoutHelper.IsPaidOrOverpaid(o.PaymentStatus)
                                                   && o.Items.Any(i => i.CompletedUtc is null)).ToList(),
                _ => orders.Where(o => SponsoredListingCheckoutHelper.IsPaidOrOverpaid(o.PaymentStatus)).ToList(),
            };

            this.ViewBag.Filter = filter;
            this.ViewBag.AttentionCount = orders.Count(NeedsAttention);
            return this.View(shown);
        }

        [HttpGet("orders/{id:int}")]
        public async Task<IActionResult> Order(int id, CancellationToken ct)
        {
            var order = await this.repository.GetOrderAsync(id, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            // Pick up a payment the webhook may not have delivered yet.
            await this.orderProcessor.SyncFromProcessorAsync(order, ct);
            return this.View(order);
        }

        [HttpPost("orders/{id:int}/mark-paid")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id, CancellationToken ct)
        {
            var order = await this.repository.GetOrderAsync(id, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            if (SponsoredListingCheckoutHelper.IsPaidOrOverpaid(order.PaymentStatus))
            {
                this.TempData[StringConstants.ErrorMessage] = "This order is already paid.";
            }
            else
            {
                await this.orderProcessor.ApplyStatusAsync(order, PaymentStatus.Paid, force: true, ct: ct);
                this.TempData[StringConstants.SuccessMessage] = order.HasConflict
                    ? "Marked as paid, but one of its videos already belongs to another sponsor. See the warning below."
                    : "Marked as paid.";
            }

            return this.RedirectToAction(nameof(this.Order), new { id });
        }

        [HttpPost("orders/{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, bool refunded, CancellationToken ct)
        {
            var order = await this.repository.GetOrderAsync(id, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            if (order.Items.Any(i => i.CompletedUtc is not null))
            {
                this.TempData[StringConstants.ErrorMessage] = "A video on this order is already completed, so the order can't be canceled.";
                return this.RedirectToAction(nameof(this.Order), new { id });
            }

            // Free the videos again (queue entries a paid order had claimed go back on offer;
            // a dedicated entry created for this order is retired).
            foreach (var item in order.Items.Where(i => i.VideoIdea is not null))
            {
                var idea = item.VideoIdea!;
                if (idea.SponsoredByOrderId == order.VideoSponsorOrderId)
                {
                    idea.SponsoredByOrderId = null;
                    if (idea.IsDedicated)
                    {
                        idea.Status = VideoIdeaStatus.Retired;
                    }
                }
            }

            await this.orderProcessor.ApplyStatusAsync(
                order, refunded ? PaymentStatus.Refunded : PaymentStatus.Canceled, force: true, ct: ct);

            this.TempData[StringConstants.SuccessMessage] = refunded
                ? "Order marked as refunded and its videos released."
                : "Order canceled and its videos released.";
            return this.RedirectToAction(nameof(this.Order), new { id });
        }

        // ---- Per-video: save the URLs (all videos in one form), then complete ----
        // A saved URL shows on the public page straight away; Complete only
        // records delivery and sends the buyer's one-time email.
        [HttpPost("orders/{id:int}/save-urls")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItemUrls(int id, Dictionary<int, string?> videoUrls, string? adminNote, CancellationToken ct)
        {
            var order = await this.repository.GetOrderAsync(id, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            var (saved, errors) = await this.ApplyPostedEditsAsync(order, videoUrls, adminNote, ct);

            if (errors.Count > 0)
            {
                this.TempData[StringConstants.ErrorMessage] = string.Join(" ", errors);
            }

            if (saved > 0 || errors.Count == 0)
            {
                this.TempData[StringConstants.SuccessMessage] = saved switch
                {
                    0 => "No changes to save.",
                    1 => "1 change saved.",
                    _ => $"{saved} changes saved.",
                };
            }

            return this.RedirectToAction(nameof(this.Order), new { id });
        }

        // Saves everything typed on the order page (every video URL and the admin note), so the
        // Complete and Retry buttons, which submit the same form, never lose unsaved edits.
        // Returns how many values changed and any per-video errors.
        private async Task<(int Saved, List<string> Errors)> ApplyPostedEditsAsync(
            VideoSponsorOrder order, Dictionary<int, string?>? videoUrls, string? adminNote, CancellationToken ct)
        {
            videoUrls ??= new Dictionary<int, string?>();
            var errors = new List<string>();
            var saved = 0;
            foreach (var item in order.Items)
            {
                if (!videoUrls.TryGetValue(item.VideoSponsorOrderItemId, out var raw))
                {
                    continue;
                }

                var videoUrl = (raw ?? string.Empty).Trim();
                var title = item.VideoIdea?.Title ?? item.Title;
                if (videoUrl.Length > 0 && (videoUrl.Length > 500 || !IsHttpUrl(videoUrl)))
                {
                    errors.Add($"\"{title}\": the video URL must start with http:// or https://.");
                    continue;
                }

                if (item.CompletedUtc is not null && videoUrl.Length == 0)
                {
                    errors.Add($"\"{title}\": a completed video needs its URL.");
                    continue;
                }

                var newUrl = videoUrl.Length > 0 ? videoUrl : null;
                if (newUrl == item.VideoUrl)
                {
                    continue;
                }

                item.VideoUrl = newUrl;
                if (item.VideoIdea is not null)
                {
                    item.VideoIdea.VideoUrl = newUrl;
                }

                saved++;
            }

            // The note field is only on the page form when it is posted (null = not posted).
            if (adminNote is not null)
            {
                var note = adminNote.Trim();
                var newNote = note.Length == 0 ? null : (note.Length > 2000 ? note[..2000] : note);
                if (newNote != order.AdminNote)
                {
                    order.AdminNote = newNote;
                    saved++;
                }
            }

            if (saved > 0)
            {
                await this.repository.UpdateOrderAsync(order, ct);
            }

            return (saved, errors);
        }

        [HttpPost("items/{itemId:int}/complete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteItem(int itemId, Dictionary<int, string?> videoUrls, string? adminNote, CancellationToken ct)
        {
            var item = await this.repository.GetItemAsync(itemId, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            var redirect = this.RedirectToAction(nameof(this.Order), new { id = item.VideoSponsorOrderId });

            // Save what was typed first, so the email carries the URL on screen, not an old one.
            var order = await this.repository.GetOrderAsync(item.VideoSponsorOrderId, ct);
            if (order is null)
            {
                return this.NotFound();
            }

            var (_, editErrors) = await this.ApplyPostedEditsAsync(order, videoUrls, adminNote, ct);
            if (editErrors.Count > 0)
            {
                this.TempData[StringConstants.ErrorMessage] = "Nothing was completed. " + string.Join(" ", editErrors);
                return redirect;
            }

            item = order.Items.FirstOrDefault(i => i.VideoSponsorOrderItemId == itemId) ?? item;

            if (!SponsoredListingCheckoutHelper.IsPaidOrOverpaid(order.PaymentStatus))
            {
                this.TempData[StringConstants.ErrorMessage] = "Only a paid order can be completed.";
                return redirect;
            }

            if (item.CompletedUtc is not null)
            {
                this.TempData[StringConstants.ErrorMessage] = "This video is already completed.";
                return redirect;
            }

            if (string.IsNullOrWhiteSpace(item.VideoUrl))
            {
                this.TempData[StringConstants.ErrorMessage] = "Save the video URL first.";
                return redirect;
            }

            item.CompletedUtc = DateTime.UtcNow;
            if (item.VideoIdea is not null)
            {
                item.VideoIdea.Status = VideoIdeaStatus.Completed;
                item.VideoIdea.VideoUrl = item.VideoUrl;
            }

            await this.repository.UpdateItemAsync(item, ct);

            var emailOutcome = await this.SendCompletionEmailAsync(item, ct);
            this.TempData[StringConstants.SuccessMessage] = $"Video marked complete. {emailOutcome}";
            return redirect;
        }

        // Only offered when a video is complete but its email never went out (send failed).
        [HttpPost("items/{itemId:int}/send-email")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendItemEmail(int itemId, Dictionary<int, string?> videoUrls, string? adminNote, CancellationToken ct)
        {
            var item = await this.repository.GetItemAsync(itemId, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            var order = await this.repository.GetOrderAsync(item.VideoSponsorOrderId, ct);
            if (order is not null)
            {
                var (_, editErrors) = await this.ApplyPostedEditsAsync(order, videoUrls, adminNote, ct);
                if (editErrors.Count > 0)
                {
                    this.TempData[StringConstants.ErrorMessage] = "The email was not sent. " + string.Join(" ", editErrors);
                    return this.RedirectToAction(nameof(this.Order), new { id = item.VideoSponsorOrderId });
                }

                item = order.Items.FirstOrDefault(i => i.VideoSponsorOrderItemId == itemId) ?? item;
            }

            if (item.CompletedUtc is null || item.CompletionEmailSentUtc is not null)
            {
                this.TempData[StringConstants.ErrorMessage] = "The email for this video has already been sent, or the video isn't complete yet.";
            }
            else
            {
                this.TempData[StringConstants.SuccessMessage] = await this.SendCompletionEmailAsync(item, ct);
            }

            return this.RedirectToAction(nameof(this.Order), new { id = item.VideoSponsorOrderId });
        }

        // =====================================================================
        // SPONSORS + REPORTS
        // =====================================================================
        [HttpGet("sponsors")]
        public async Task<IActionResult> Sponsors(CancellationToken ct)
        {
            var paid = await this.repository.ListPaidOrdersAsync(ct);

            var rows = paid
                .GroupBy(o => o.DirectoryEntryId)
                .Select(g => new VideoSponsorSummaryRow
                {
                    DirectoryEntryId = g.Key,
                    ListingName = g.First().DirectoryEntry?.Name ?? $"Listing {g.Key}",
                    DirectoryEntryKey = g.First().DirectoryEntry?.DirectoryEntryKey ?? string.Empty,
                    Orders = g.Count(),
                    Videos = g.Sum(o => o.Items.Count),
                    VideosDelivered = g.Sum(o => o.Items.Count(i => i.CompletedUtc is not null)),
                    TotalPaidUsd = g.Sum(o => o.Amount),
                    DemoCostsUsd = g.Sum(o => o.DemoCostUsd),
                    FirstPaidUtc = g.Min(o => o.PaidUtc),
                    LastPaidUtc = g.Max(o => o.PaidUtc),
                    Email = g.OrderByDescending(o => o.PaidUtc).Select(o => o.Email).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                })
                .OrderByDescending(r => r.LastPaidUtc)
                .ToList();

            return this.View(rows);
        }

        [HttpGet("reports")]
        public async Task<IActionResult> Reports(CancellationToken ct)
        {
            var paid = await this.repository.ListPaidOrdersAsync(ct);
            var now = DateTime.UtcNow;
            var deliveryDays = Common.Constants.IntegerConstants.VideoSponsorDeliveryDays;

            static VideoSponsorReportRow ToRow(string label, IEnumerable<VideoSponsorOrder> orders)
            {
                var list = orders.ToList();
                return new VideoSponsorReportRow
                {
                    Label = label,
                    Orders = list.Count,
                    Videos = list.Sum(o => o.Items.Count),
                    RevenueUsd = list.Sum(o => o.Amount - o.DemoCostUsd),
                    DemoCostsUsd = list.Sum(o => o.DemoCostUsd),
                    DiscountsUsd = list.Sum(o => o.DiscountAmount),
                    PaidXmr = list.Sum(o => o.PaidAmount),
                };
            }

            var outstanding = paid
                .SelectMany(o => o.Items.Where(i => i.CompletedUtc is null).Select(i => o.PaidUtc ?? o.CreateDate))
                .ToList();

            var vm = new VideoSponsorReportVm
            {
                TotalRevenueUsd = paid.Sum(o => o.Amount - o.DemoCostUsd),
                TotalDemoCostsUsd = paid.Sum(o => o.DemoCostUsd),
                TotalDiscountsUsd = paid.Sum(o => o.DiscountAmount),
                TotalPaidXmr = paid.Sum(o => o.PaidAmount),
                TotalOrders = paid.Count,
                TotalVideos = paid.Sum(o => o.Items.Count),
                VideosDelivered = paid.Sum(o => o.Items.Count(i => i.CompletedUtc is not null)),
                VideosOutstanding = outstanding.Count,
                VideosOverdue = outstanding.Count(paidUtc => paidUtc.AddDays(deliveryDays) < now),
                ByMonth = paid
                    .GroupBy(o => (o.PaidUtc ?? o.CreateDate).ToString("yyyy-MM", CultureInfo.InvariantCulture))
                    .OrderByDescending(g => g.Key)
                    .Select(g => ToRow(g.Key, g))
                    .ToList(),
                ByType = paid
                    .GroupBy(o => o.OfferName)
                    .OrderByDescending(g => g.Sum(o => o.Amount))
                    .Select(g => ToRow(g.Key, g))
                    .ToList(),
            };

            return this.View(vm);
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        // Orders the admin should look at: payment problems, or paid for a video that someone
        // else already owns.
        private static bool NeedsAttention(VideoSponsorOrder o) =>
            o.PaymentStatus is PaymentStatus.UnderPayment or PaymentStatus.Failed || o.HasConflict;

        private static bool IsTrue(string? value) =>
            string.Equals((value ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase);

        private static bool IsHttpUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private void SaveSnippet(SiteConfigSetting type, string content)
        {
            var existing = this.contentSnippetRepository.Get(type);
            if (existing is null)
            {
                this.contentSnippetRepository.Create(new ContentSnippet { SnippetType = type, Content = content });
            }
            else
            {
                existing.Content = content;
                this.contentSnippetRepository.Update(existing);
            }

            this.cacheService.ClearSnippetCache(type);
        }

        // Sends the one-time "your video is live" email and records the outcome on the item.
        // EmailService also writes every attempt to the EmailSendLog audit table.
        private async Task<string> SendCompletionEmailAsync(VideoSponsorOrderItem item, CancellationToken ct)
        {
            var order = item.VideoSponsorOrder;
            string outcome;

            if (string.IsNullOrWhiteSpace(order.Email))
            {
                item.CompletionEmailResult = "No email on the order; nothing sent.";
                outcome = "The buyer left no email, so no email was sent.";
            }
            else
            {
                var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
                var title = item.VideoIdea?.Title ?? item.Title;
                var listing = order.DirectoryEntry?.Name ?? "your listing";
                var subject = $"{siteName}: your sponsored video is live";

                var text =
                    $"Hello,\n\nThe video you sponsored for {listing} is now published.\n\n" +
                    $"Video: {title}\nWatch: {item.VideoUrl}\n\nOrder ID: {order.InvoiceId}\n\n" +
                    $"Thank you for sponsoring {siteName}.";

                var html =
                    $"<p>Hello,</p><p>The video you sponsored for <strong>{WebUtility.HtmlEncode(listing)}</strong> is now published.</p>" +
                    $"<p><strong>Video:</strong> {WebUtility.HtmlEncode(title)}<br />" +
                    $"<strong>Watch:</strong> <a href=\"{WebUtility.HtmlEncode(item.VideoUrl)}\">{WebUtility.HtmlEncode(item.VideoUrl)}</a></p>" +
                    $"<p>Order ID: {order.InvoiceId}</p><p>Thank you for sponsoring {WebUtility.HtmlEncode(siteName)}.</p>";

                try
                {
                    // Resolved only when actually sending, so the admin pages never depend on
                    // the mail configuration just to load.
                    var emailService = this.HttpContext.RequestServices.GetRequiredService<IEmailService>();
                    await emailService.SendEmailAsync(subject, text, html, new List<string> { order.Email });
                    item.CompletionEmailSentUtc = DateTime.UtcNow;
                    item.CompletionEmailResult = $"Sent to {order.Email}";
                    outcome = $"Email sent to {order.Email}.";
                }
                catch (Exception ex)
                {
                    var reason = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                    item.CompletionEmailResult = $"Send failed: {reason}";
                    outcome = "The email could NOT be sent (see the order page to retry).";
                }
            }

            await this.repository.UpdateItemAsync(item, ct);
            return outcome;
        }
    }
}
