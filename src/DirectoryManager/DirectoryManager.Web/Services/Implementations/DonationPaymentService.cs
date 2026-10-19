using BtcPayServer.API.Interfaces;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Called by the donations-store webhook. Looks the invoice up once and records the
    /// payment as soon as BTCPay has detected it (Processing counts), including a payment that
    /// landed after the invoice expired (PaidLate). No page ever polls BTCPay.
    /// </summary>
    public class DonationPaymentService : IDonationPaymentService
    {
        private readonly IBtcPayServerService btcPay;
        private readonly ISubmissionRepository submissions;
        private readonly IVerificationRequestRepository verifications;
        private readonly IProblemReportRepository problems;

        public DonationPaymentService(
            IBtcPayServerService btcPay,
            ISubmissionRepository submissions,
            IVerificationRequestRepository verifications,
            IProblemReportRepository problems)
        {
            this.btcPay = btcPay;
            this.submissions = submissions;
            this.verifications = verifications;
            this.problems = problems;
        }

        public async Task<bool> RecordPaymentAsync(string invoiceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
            {
                return false;
            }

            var submissionId = await this.submissions.GetUnpaidIdByInvoiceIdAsync(invoiceId, ct);
            var verificationId = submissionId is null ? await this.verifications.GetUnpaidIdByInvoiceIdAsync(invoiceId, ct) : null;
            var problemId = submissionId is null && verificationId is null ? await this.problems.GetUnpaidIdByInvoiceIdAsync(invoiceId, ct) : null;
            if (submissionId is null && verificationId is null && problemId is null)
            {
                return false;
            }

            var paid = await this.ReadPaymentAsync(invoiceId);
            if (paid is null)
            {
                return false;
            }

            var (amount, currency, paidUsd, paidUtc) = paid.Value;
            if (submissionId is int s)
            {
                await this.submissions.SetPaidAsync(s, amount, currency, paidUsd, paidUtc);
            }
            else if (verificationId is int v)
            {
                await this.verifications.SetPaidAsync(v, amount, currency, paidUsd, paidUtc, ct);
            }
            else if (problemId is int p)
            {
                await this.problems.SetPaidAsync(p, amount, currency, paidUsd, paidUtc, ct);
            }

            return true;
        }

        // Amount, currency, USD value and received time of a paid donation invoice, or null if
        // BTCPay hasn't seen a payment for it yet.
        private async Task<(decimal? Amount, string? Currency, decimal? PaidUsd, DateTime PaidUtc)?> ReadPaymentAsync(string invoiceId)
        {
            var storeId = this.btcPay.ReviewDonationsStoreId;
            if (string.IsNullOrWhiteSpace(storeId))
            {
                return null;
            }

            var invoice = await this.btcPay.GetInvoiceOnStoreAsync(storeId, invoiceId);

            // Donations are pay-what-you-want, so count funds that arrived even if the
            // invoice window had expired by the time the payment came in (PaidLate).
            if (!invoice.IsPaidOrLate)
            {
                return null;
            }

            decimal? amount = decimal.TryParse(invoice.Amount, out var a) && a > 0 ? a : null;
            var currency = string.IsNullOrWhiteSpace(invoice.Currency) ? null : invoice.Currency;

            // The XMR payment method carries the actual received time of each payment and, for
            // top-up invoices that report an invoice amount of 0, the actual XMR received.
            string? xmrRateUsd = null;
            var xmr = await this.btcPay.GetXmrPaymentMethodOnStoreAsync(storeId, invoiceId);
            var receivedUtc = xmr?.Payments
                .Where(p => p.ReceivedDate > 0)
                .Select(p => (DateTime?)p.ReceivedDateUtc)
                .Max();
            if (amount is null && xmr is not null && decimal.TryParse(xmr.TotalPaid, out var xmrPaid) && xmrPaid > 0)
            {
                amount = xmrPaid;
                currency = "XMR";
                xmrRateUsd = xmr.Rate;
            }

            var paidUsd = DonationUsdHelper.ToUsd(amount, currency, xmrRateUsd);
            return (amount, currency, paidUsd, receivedUtc ?? DateTime.UtcNow);
        }
    }
}
