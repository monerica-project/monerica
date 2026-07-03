using DirectoryManager.Data.Enums;
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

        public VerificationRequestsAdminController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            IVerificationRequestRepository requests,
            IDirectoryEntryRepository entryRepo)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.requests = requests;
            this.entryRepo = entryRepo;
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
