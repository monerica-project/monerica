using System.Globalization;
using BtcPayServer.API.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>What BTCPay's own record says about a sponsorship invoice that is not paid on our side.</summary>
    public enum LatePaymentState
    {
        /// <summary>Nothing to do: not paid, or not an expired-then-paid invoice.</summary>
        NotApplicable = 0,

        /// <summary>BTCPay shows the invoice settled (a missed webhook, or marked settled by hand).</summary>
        SettledAtProcessor = 1,

        /// <summary>Paid in full after the timer ran out, but not every payment is confirmed yet.</summary>
        WaitingForConfirmations = 2,

        /// <summary>Paid in full after the timer ran out, confirmed, and inside the grace period.</summary>
        ReadyToAccept = 3,

        /// <summary>Paid in full, but later than the grace period allows: the owner decides.</summary>
        TooLate = 4,
    }

    public sealed record LatePaymentCheck(LatePaymentState State, decimal SettledXmr, DateTime? LastReceivedUtc, double MinutesLate);

    /// <summary>
    /// Decides whether a payment that reached BTCPay AFTER the invoice timer ran out can be
    /// accepted automatically. BTCPay never settles such an invoice on its own (it stays
    /// "Expired" with the extra status "PaidLate"), so without this the buyer has paid and
    /// nothing goes live until the owner marks the invoice settled by hand.
    /// Pure: reads BTCPay's invoice and payment-method records, touches nothing.
    /// </summary>
    public static class LatePaymentRules
    {
        /// <summary>How long after the timer ran out a full payment is still accepted automatically.</summary>
        public static readonly TimeSpan DefaultGrace = TimeSpan.FromMinutes(60);

        /// <summary>Same 1% payment tolerance the BTCPay store uses.</summary>
        public const decimal Tolerance = 0.01m;

        public static LatePaymentCheck Evaluate(BtcPayInvoiceResponse? invoice, BtcPayPaymentMethod? method, TimeSpan grace)
        {
            var none = new LatePaymentCheck(LatePaymentState.NotApplicable, 0m, null, 0);
            if (invoice == null)
            {
                return none;
            }

            if (invoice.IsSettled)
            {
                return none with { State = LatePaymentState.SettledAtProcessor };
            }

            if (invoice.Status != "Expired" || invoice.AdditionalStatus != "PaidLate" || method == null)
            {
                return none;
            }

            var due = Parse(method.Amount);
            if (due <= 0m || invoice.ExpirationTime <= 0)
            {
                return none;
            }

            var payments = (method.Payments ?? new List<BtcPayPaymentEntry>())
                .Where(p => !string.Equals(p.Status, "Invalid", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (payments.Count == 0)
            {
                return none;
            }

            var needed = due * (1m - Tolerance);
            var settled = payments.Where(p => string.Equals(p.Status, "Settled", StringComparison.OrdinalIgnoreCase)).Sum(p => Parse(p.Value));
            var seen = payments.Sum(p => Parse(p.Value));
            if (seen < needed)
            {
                return none;
            }

            var lastReceived = payments.Max(p => p.ReceivedDateUtc);
            var minutesLate = Math.Max(0, (lastReceived - invoice.ExpirationTimeUtc).TotalMinutes);
            var result = new LatePaymentCheck(LatePaymentState.ReadyToAccept, settled, lastReceived, minutesLate);

            if (lastReceived > invoice.ExpirationTimeUtc + grace)
            {
                return result with { State = LatePaymentState.TooLate };
            }

            return settled >= needed ? result : result with { State = LatePaymentState.WaitingForConfirmations };
        }

        private static decimal Parse(string? value) =>
            decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
