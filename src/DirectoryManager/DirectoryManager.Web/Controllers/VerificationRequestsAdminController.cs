using BtcPayServer.API.Interfaces;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
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
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly ICategoryRepository categoryRepo;

        public VerificationRequestsAdminController(
            IMemoryCache cache,
            IVerificationRequestRepository requests,
            IDirectoryEntryRepository entryRepo,
            IBtcPayServerService btcPay,
            ISubcategoryRepository subcategoryRepo,
            ICategoryRepository categoryRepo)
            : base(cache)
        {
            this.requests = requests;
            this.entryRepo = entryRepo;
            this.btcPay = btcPay;
            this.subcategoryRepo = subcategoryRepo;
            this.categoryRepo = categoryRepo;
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

            return this.View(new VerificationRequestQueueViewModel
            {
                Status = status,
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// Pending requests grouped by listing, oldest listing first, so long-listed businesses
        /// (the ones that could sponsor as soon as they're verified) are easy to spot.
        /// </summary>
        [HttpGet("by-listing-age")]
        public async Task<IActionResult> ByListingAge(CancellationToken ct = default)
        {
            var pending = await this.requests.ListByStatusAsync(VerificationRequestStatus.Pending, 1, 5000, ct);
            var subs = (await this.subcategoryRepo.GetAllAsync()).ToDictionary(s => s.SubCategoryId);
            var cats = (await this.categoryRepo.GetAllAsync()).ToDictionary(c => c.CategoryId, c => c.Name);
            var now = DateTime.UtcNow;
            int minDays = DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring;

            string CategoryOf(int? subId) =>
                subId is int id && subs.TryGetValue(id, out var sc)
                    ? $"{(cats.TryGetValue(sc.CategoryId, out var cn) ? cn : "?")} › {sc.Name}"
                    : string.Empty;

            var rows = pending
                .Where(r => r.DirectoryEntry != null)
                .GroupBy(r => r.DirectoryEntryId)
                .Select(g =>
                {
                    var e = g.First().DirectoryEntry!;
                    var latest = g.OrderByDescending(r => r.CreateDate).First();
                    var eligible = e.CreateDate.Date.AddDays(minDays);
                    return new VerificationRequestAgeRow
                    {
                        DirectoryEntryId = e.DirectoryEntryId,
                        Name = e.Name,
                        DirectoryEntryKey = e.DirectoryEntryKey,
                        Category = CategoryOf(e.SubCategoryId),
                        Status = e.DirectoryStatus,
                        ListedOn = e.CreateDate,
                        DaysListed = SponsorshipEligibilityHelper.ComputeAgeDays(e.CreateDate, now),
                        CanSponsorFrom = eligible <= now.Date ? null : eligible,
                        RequestCount = g.Count(),
                        LatestRequest = latest.CreateDate,
                        LatestRequestId = latest.VerificationRequestId,
                        LatestComment = latest.Comment ?? string.Empty,
                        AnyDonation = g.Any(r => r.PaidUtc != null),
                    };
                })
                .OrderBy(r => r.ListedOn)
                .ToList();

            return this.View(new VerificationRequestsByListingAgeViewModel { Rows = rows, MinimumDaysListed = minDays });
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
