using BtcPayServer.API.Interfaces;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.ProblemReports;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.ProblemReports;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/problem-reports")]
    public class ProblemReportsAdminController : BaseController
    {
        private readonly IProblemReportRepository requests;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly IBtcPayServerService btcPay;

        public ProblemReportsAdminController(
            IMemoryCache cache,
            IProblemReportRepository requests,
            IDirectoryEntryRepository entryRepo,
            IBtcPayServerService btcPay)
            : base(cache)
        {
            this.requests = requests;
            this.entryRepo = entryRepo;
            this.btcPay = btcPay;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            ProblemReportStatus status = ProblemReportStatus.Pending,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            var items = await this.requests.ListByStatusAsync(status, page, pageSize, ct);
            var total = await this.requests.CountByStatusAsync(status, ct);

            return this.View(new ProblemReportQueueViewModel
            {
                Status = status,
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // Full detail for a single report — the queue clamps the comment to two lines,
        // so this is where the whole thing (and its listing context) can be read.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
        {
            var request = await this.requests.GetByIdAsync(id, ct);
            if (request is null)
            {
                return this.NotFound();
            }


            // If paid, surface the actual XMR amount received (USD is stored on the report).
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

            return this.View(new ProblemReportDetailViewModel
            {
                Request = request,
                Entry = entry,
            });
        }

        [HttpPost("{id:int}/reviewed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReviewed(int id, CancellationToken ct = default)
        {
            await this.requests.SetStatusAsync(id, ProblemReportStatus.Reviewed, ct);
            this.TempData["SuccessMessage"] = "Marked as reviewed.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("{id:int}/dismiss")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(int id, CancellationToken ct = default)
        {
            await this.requests.SetStatusAsync(id, ProblemReportStatus.Dismissed, ct);
            this.TempData["SuccessMessage"] = "Dismissed.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
