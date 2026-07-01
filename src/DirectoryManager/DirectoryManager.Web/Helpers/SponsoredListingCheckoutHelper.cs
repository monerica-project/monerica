using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Pure-static helpers shared by SponsoredListingController and SponsoredListingCheckoutController.
    /// No dependencies — safe to call from both controllers without coupling them.
    /// </summary>
    internal static class SponsoredListingCheckoutHelper
    {
        internal static readonly PaymentStatus[] HoldExtendingStatuses =
        {
            PaymentStatus.InvoiceCreated,
            PaymentStatus.Pending,
            PaymentStatus.UnderPayment,
        };

        internal static readonly IReadOnlyDictionary<PaymentStatus, int> StatusRank =
            new Dictionary<PaymentStatus, int>
            {
                { PaymentStatus.Unknown,        0 },
                { PaymentStatus.InvoiceCreated, 1 },
                { PaymentStatus.UnderPayment,   2 },
                { PaymentStatus.Pending,        3 },
                { PaymentStatus.OverPayment,    4 }, // settled-with-overpay; ranks just below clean Paid
                { PaymentStatus.Paid,           5 },
                { PaymentStatus.Expired,        6 },
                { PaymentStatus.Failed,         6 },
                { PaymentStatus.Refunded,       6 },
                { PaymentStatus.Canceled,       6 },
                { PaymentStatus.Test,           6 },
            };

        internal static int GetMaxSlotsForType(SponsorshipType type) => type switch
        {
            SponsorshipType.MainSponsor => Common.Constants.IntegerConstants.MaxMainSponsoredListings,
            SponsorshipType.CategorySponsor => Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
            SponsorshipType.SubcategorySponsor => Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
            _ => 0,
        };

        // OverPayment is terminal: BTCPay marks it settled, the campaign should fulfill.
        internal static bool IsTerminal(PaymentStatus s) =>
            s is PaymentStatus.Paid or PaymentStatus.OverPayment
              or PaymentStatus.Expired or PaymentStatus.Failed
              or PaymentStatus.Refunded or PaymentStatus.Canceled or PaymentStatus.Test;

        /// <summary>
        /// True when the invoice is effectively paid (exact or over) and the campaign should activate.
        /// </summary>
        /// <returns></returns>
        internal static bool IsPaidOrOverpaid(PaymentStatus s) =>
            s is PaymentStatus.Paid or PaymentStatus.OverPayment;

        internal static bool CanPurchaseListing(int totalActive, int totalReserved, SponsorshipType type)
        {
            var max = GetMaxSlotsForType(type);
            return totalActive <= max && totalReserved < (max - totalActive);
        }

        internal static PaymentStatus ConvertToInternalStatus(NowPayments.API.Enums.PaymentStatus s) => s switch
        {
            NowPayments.API.Enums.PaymentStatus.Unknown => PaymentStatus.Unknown,
            NowPayments.API.Enums.PaymentStatus.Waiting => PaymentStatus.InvoiceCreated,
            NowPayments.API.Enums.PaymentStatus.Sending
                or NowPayments.API.Enums.PaymentStatus.Confirming
                or NowPayments.API.Enums.PaymentStatus.Confirmed => PaymentStatus.Pending,
            NowPayments.API.Enums.PaymentStatus.Finished => PaymentStatus.Paid,
            NowPayments.API.Enums.PaymentStatus.PartiallyPaid => PaymentStatus.UnderPayment,
            NowPayments.API.Enums.PaymentStatus.Failed
                or NowPayments.API.Enums.PaymentStatus.Refunded => PaymentStatus.Failed,
            NowPayments.API.Enums.PaymentStatus.Expired => PaymentStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(s), s, null),
        };

        /// <summary>
        /// Maps a BTCPay webhook event onto our internal PaymentStatus.
        /// Standard BTCPay event types: InvoiceCreated, InvoiceReceivedPayment, InvoiceProcessing,
        /// InvoiceExpired, InvoiceSettled, InvoiceInvalid, InvoicePaymentSettled.
        ///
        /// NOTE: InvoiceSettled always maps to Paid, even when BTCPay reports OverPaid=true.
        /// Sub-cent rounding overpayments were getting flagged as OverPayment and breaking
        /// activation. The OverPaid flag is still preserved in invoice.PaymentResponse (full
        /// webhook JSON) and visible in BTCPay Server itself, so genuine large overpayments
        /// can still be audited. PaymentStatus.OverPayment remains a valid terminal state
        /// elsewhere in the system in case it's ever assigned through another path.
        /// </summary>
        /// <param name="eventType">The BTCPay event Type string (case-insensitive).</param>
        /// <param name="partiallyPaid">From InvoiceExpired payloads.</param>
        /// <param name="amountDue">Optional: remaining due reported by BTCPay (for InvoiceReceivedPayment fallback).</param>
        /// <returns></returns>
        internal static PaymentStatus MapBtcPayEventToInternalStatus(
            string? eventType,
            bool partiallyPaid = false,
            decimal? amountDue = null)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return PaymentStatus.Unknown;
            }

            return eventType.Trim() switch
            {
                var t when t.Equals("InvoiceCreated", StringComparison.OrdinalIgnoreCase)
                    => PaymentStatus.InvoiceCreated,

                // Partial on the wire — only treat as UnderPayment if there's still amount due.
                // If amountDue is unknown, default to Pending (full receipt path will refine it).
                var t when t.Equals("InvoiceReceivedPayment", StringComparison.OrdinalIgnoreCase)
                    => amountDue.HasValue && amountDue.Value > 0m
                        ? PaymentStatus.UnderPayment
                        : PaymentStatus.Pending,

                // Fully received, awaiting on-chain confirmations.
                var t when t.Equals("InvoiceProcessing", StringComparison.OrdinalIgnoreCase)
                    => PaymentStatus.Pending,

                // Settled — always Paid. See note above re: overpayment handling.
                var t when t.Equals("InvoiceSettled", StringComparison.OrdinalIgnoreCase)
                    => PaymentStatus.Paid,

                // BTCPay also emits this on a per-payment-method confirmation.
                // Treat as Pending — InvoiceSettled is the authoritative finisher.
                var t when t.Equals("InvoicePaymentSettled", StringComparison.OrdinalIgnoreCase)
                    => PaymentStatus.Pending,

                // Expired — distinguish expired-with-partial vs. clean expiry.
                var t when t.Equals("InvoiceExpired", StringComparison.OrdinalIgnoreCase)
                    => partiallyPaid ? PaymentStatus.UnderPayment : PaymentStatus.Expired,

                var t when t.Equals("InvoiceInvalid", StringComparison.OrdinalIgnoreCase)
                    => PaymentStatus.Failed,

                _ => PaymentStatus.Unknown,
            };
        }

        /// <summary>Resolves the integer ID used to key reservation groups and capacity checks.</summary>
        /// <returns></returns>
        internal static int ResolveTypeIdForGroup(
            SponsorshipType type,
            DirectoryEntry entry,
            int? subCategoryId,
            int? categoryId) => type switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => categoryId ?? entry.SubCategory?.CategoryId ?? 0,
                SponsorshipType.SubcategorySponsor => subCategoryId ?? entry.SubCategoryId,
                _ => 0,
            };
    }
}