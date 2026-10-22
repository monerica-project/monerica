using System.Globalization;
using BtcPayServer.API.Models;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VideoSponsorships;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class VideoSponsorOrderProcessor : IVideoSponsorOrderProcessor
    {
        // Once money is on its way (or partly arrived) the videos stay locked this long, well
        // past the invoice window, so a slow confirmation can never free them for another buyer.
        private static readonly TimeSpan PaymentInFlightHold = TimeSpan.FromHours(24);

        private readonly IVideoSponsorshipRepository repository;
        private readonly IVideoSponsorPaymentGateway gateway;
        private readonly ILogger<VideoSponsorOrderProcessor> logger;

        public VideoSponsorOrderProcessor(
            IVideoSponsorshipRepository repository,
            IVideoSponsorPaymentGateway gateway,
            ILogger<VideoSponsorOrderProcessor> logger)
        {
            this.repository = repository;
            this.gateway = gateway;
            this.logger = logger;
        }

        public static PaymentStatus MapProcessorInvoiceStatus(string? status, string? additionalStatus)
        {
            var s = (status ?? string.Empty).Trim();

            if (s.Equals("Settled", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatus.Paid;
            }

            if (s.Equals("Processing", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatus.Pending;
            }

            if (s.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            {
                return (additionalStatus ?? string.Empty).Contains("PaidPartial", StringComparison.OrdinalIgnoreCase)
                    ? PaymentStatus.UnderPayment
                    : PaymentStatus.Expired;
            }

            if (s.Equals("Invalid", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatus.Failed;
            }

            return PaymentStatus.Unknown;
        }

        public async Task ApplyStatusAsync(
            VideoSponsorOrder order, PaymentStatus proposed, string? paymentResponseJson = null, bool force = false, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(paymentResponseJson))
            {
                order.PaymentResponse = paymentResponseJson;
            }

            var current = order.PaymentStatus;
            var isStepForward = proposed != PaymentStatus.Unknown
                && !SponsoredListingCheckoutHelper.IsTerminal(current)
                && (SponsoredListingCheckoutHelper.StatusRank[proposed] > SponsoredListingCheckoutHelper.StatusRank[current]

                    // partly paid when the window closed: record it (see CanAdvanceStatus)
                    || (current == PaymentStatus.Pending && proposed == PaymentStatus.UnderPayment));

            if (!isStepForward && !(force && proposed != current))
            {
                await this.repository.UpdateOrderAsync(order, ct);
                return;
            }

            order.PaymentStatus = proposed;

            if (proposed is PaymentStatus.Pending or PaymentStatus.UnderPayment)
            {
                await this.PopulatePaymentAmountsAsync(order);
                await this.repository.SetHoldExpiryAsync(order.VideoSponsorOrderId, DateTime.UtcNow.Add(PaymentInFlightHold), ct);
            }
            else if (SponsoredListingCheckoutHelper.IsPaidOrOverpaid(proposed))
            {
                await this.PopulatePaymentAmountsAsync(order);
                order.PaidUtc ??= DateTime.UtcNow;

                var dedicatedTitle = $"Dedicated video: {order.DirectoryEntry?.Name ?? "sponsor"}";
                var allAssigned = await this.repository.AssignVideosToPaidOrderAsync(order.VideoSponsorOrderId, dedicatedTitle, ct);
                if (!allAssigned)
                {
                    order.HasConflict = true;
                    this.logger.LogWarning(
                        "Video sponsor order {OrderId} was paid but one of its videos is already sponsored by another order.",
                        order.VideoSponsorOrderId);
                }
            }
            else
            {
                // Expired / failed / canceled / refunded: free the videos for other buyers.
                await this.repository.ReleaseHoldsAsync(order.VideoSponsorOrderId, ct);
            }

            await this.repository.UpdateOrderAsync(order, ct);
        }

        public async Task SyncFromProcessorAsync(VideoSponsorOrder order, CancellationToken ct = default)
        {
            var client = this.gateway.Client;
            if (client is null
                || string.IsNullOrWhiteSpace(order.ProcessorInvoiceId)
                || SponsoredListingCheckoutHelper.IsTerminal(order.PaymentStatus))
            {
                return;
            }

            try
            {
                var invoice = await client.GetInvoiceAsync(order.ProcessorInvoiceId);
                var proposed = MapProcessorInvoiceStatus(invoice.Status, invoice.AdditionalStatus);
                if (proposed != PaymentStatus.Unknown)
                {
                    await this.ApplyStatusAsync(order, proposed, ct: ct);
                }
            }
            catch (Exception ex)
            {
                // A BTCPay hiccup must never break the page that asked; the webhook still lands.
                this.logger.LogWarning(ex, "Could not sync video sponsor order {OrderId} from BTCPay.", order.VideoSponsorOrderId);
            }
        }

        private async Task PopulatePaymentAmountsAsync(VideoSponsorOrder order)
        {
            var client = this.gateway.Client;
            if (client is null || string.IsNullOrWhiteSpace(order.ProcessorInvoiceId))
            {
                return;
            }

            try
            {
                BtcPayPaymentMethod? xmr = await client.GetXmrPaymentMethodAsync(order.ProcessorInvoiceId);
                if (xmr is null)
                {
                    return;
                }

                order.PaidInCurrency = Currency.XMR;
                if (TryParsePositive(xmr.Rate, out var rate))
                {
                    order.PaymentRate = rate;
                }

                foreach (var candidate in new[] { xmr.PaymentMethodPaid, xmr.TotalPaid })
                {
                    if (TryParsePositive(candidate, out var paid))
                    {
                        order.PaidAmount = paid;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Could not read XMR amounts for video sponsor order {OrderId}.", order.VideoSponsorOrderId);
            }
        }

        private static bool TryParsePositive(string? value, out decimal parsed) =>
            decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) && parsed > 0m;
    }
}
