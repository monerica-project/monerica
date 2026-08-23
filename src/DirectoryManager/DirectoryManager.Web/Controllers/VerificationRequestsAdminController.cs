using BtcPayServer.API.Interfaces;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.VerificationRequests;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/verification-requests")]
    public class VerificationRequestsAdminController : BaseController
    {
        private readonly IVerificationRequestRepository requests;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly IBtcPayServerService btcPay;

        public VerificationRequestsAdminController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            IVerificationRequestRepository requests,
            IDirectoryEntryRepository entryRepo,
            IBtcPayServerService btcPay)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.requests = requests;
            this.entryRepo = entryRepo;
            this.btcPay = btcPay;
        }

        // Polls BTCPay for a request that has an invoice but isn't marked paid yet, and
        // persists the paid state once a payment is seen. Best-effort: a BTCPay hiccup must
        // never break the admin queue, so everything here is wrapped and swallowed.
        private async Task SyncPaymentAsync(VerificationRequest item, CancellationToken ct)
        {
            if (item.PaidUtc is not null || string.IsNullOrWhiteSpace(item.BtcPayInvoiceId))
            {
                return;
            }

            var storeId = this.btcPay.ReviewDonationsStoreId;
            if (string.IsNullOrWhiteSpace(storeId))
            {
                return;
            }

            try
            {
                var invoice = await this.btcPay.GetInvoiceOnStoreAsync(storeId, item.BtcPayInvoiceId!);

                // Donations are pay-what-you-want, so count funds that arrived even if the
                // invoice window had expired by the time the payment confirmed (PaidLate).
                if (!invoice.IsPaidOrLate)
                {
                    return;
                }

                decimal? amount = decimal.TryParse(invoice.Amount, out var a) && a > 0 ? a : null;
                var currency = string.IsNullOrWhiteSpace(invoice.Currency) ? null : invoice.Currency;

                // Top-up invoices report an invoice amount of 0, so record the actual XMR received.
                if (amount is null)
                {
                    var xmr = await this.btcPay.GetXmrPaymentMethodOnStoreAsync(storeId, item.BtcPayInvoiceId!);
                    if (xmr is not null && decimal.TryParse(xmr.TotalPaid, out var xmrPaid) && xmrPaid > 0)
                    {
                        amount = xmrPaid;
                        currency = "XMR";
                    }
                }

                var paidUtc = DateTime.UtcNow;

                await this.requests.SetPaidAsync(item.VerificationRequestId, amount, currency, paidUtc, ct);

                // reflect it in the object we're about to render
                item.PaidUtc = paidUtc;
                item.PaidAmount = amount;
                item.PaidCurrency = currency;
            }
            catch
            {
                // ignore — payment status just won't update this load
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            VerificationRequestStatus status = VerificationRequestStatus.Pending,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            var items = await this.requests.ListByStatusAsync(status, page, pageSize, ct);
            var total = await this.requests.CountByStatusAsync(status, ct);

            // Refresh payment status for any request with an invoice that isn't paid yet.
            foreach (var item in items)
            {
                await this.SyncPaymentAsync(item, ct);
            }

            return this.View(new VerificationRequestQueueViewModel
            {
                Status = status,
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // Full detail for a single request — the queue clamps the comment to two lines,
        // so this is where the whole thing (and its listing context) can be read.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
        {
            var request = await this.requests.GetByIdAsync(id, ct);
            if (request is null)
            {
                return this.NotFound();
            }

            await this.SyncPaymentAsync(request, ct);

            // If paid, surface the actual XMR amount received (USD is stored on the request).
            if (request.PaidUtc is not null && !string.IsNullOrWhiteSpace(request.BtcPayInvoiceId))
            {
                try
                {
                    var xmr = await this.btcPay.GetXmrPaymentMethodOnStoreAsync(
                        this.btcPay.ReviewDonationsStoreId, request.BtcPayInvoiceId!);
                    if (xmr is not null && decimal.TryParse(xmr.TotalPaid, out var xmrPaid) && xmrPaid > 0)
                    {
                        this.ViewBag.PaidXmr = xmrPaid;
                    }
                }
                catch
                {
                    // best-effort — the XMR amount just won't show
                }
            }

            var entry = await this.entryRepo.GetByIdAsync(request.DirectoryEntryId);

            return this.View(new VerificationRequestDetailViewModel
            {
                Request = request,
                Entry = entry,
            });
        }

        [HttpPost("{id:int}/reviewed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReviewed(int id, CancellationToken ct = default)
        {
            await this.requests.SetStatusAsync(id, VerificationRequestStatus.Reviewed, ct);
            this.TempData["SuccessMessage"] = "Marked as reviewed.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("{id:int}/dismiss")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id, CancellationToken ct = default)
        {
            await this.requests.SetStatusAsync(id, VerificationRequestStatus.Dismissed, ct);
            this.TempData["SuccessMessage"] = "Dismissed.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
