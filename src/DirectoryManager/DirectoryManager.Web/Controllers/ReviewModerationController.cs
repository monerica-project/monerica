using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/reviews")]
    public class ReviewModerationController : Controller
    {
        private readonly IDirectoryEntryReviewRepository repo;

        public ReviewModerationController(IDirectoryEntryReviewRepository repo)
        {
            this.repo = repo;
        }

        // GET /admin/reviews  -> Pending queue
        [HttpGet("")]
        public async Task<IActionResult> Pending(int page = 1, int pageSize = 50)
        {
            var items = await this.repo.ListByStatusAsync(ReviewModerationStatus.Pending, page, pageSize);
            var total = await this.repo.CountByStatusAsync(ReviewModerationStatus.Pending);

            this.ViewBag.Total = total;
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;

            return this.View("Pending", items);
        }

        // GET /admin/reviews/all?status=Approved|Rejected|Pending (optional)
        [HttpGet("all")]
        public async Task<IActionResult> All(ReviewModerationStatus? status = null, int page = 1, int pageSize = 50)
        {
            IReadOnlyList<DirectoryEntryReview> items;
            int total;

            if (status.HasValue)
            {
                items = await this.repo.ListByStatusAsync(status.Value, page, pageSize);
                total = await this.repo.CountByStatusAsync(status.Value);
            }
            else
            {
                items = await this.repo.ListAsync(page, pageSize);
                total = await this.repo.CountAsync();
            }

            this.ViewBag.Total = total;
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.Status = status;

            return this.View("All", items);
        }

        // GET /admin/reviews/123
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Show(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View("Show", item);
        }

        // POST /admin/reviews/123/approve
        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            await this.repo.ApproveAsync(id);
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/123/reject
        [HttpPost("{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, [FromForm] string? reason)
        {
            // Reason isn’t stored in the model but we can require it to avoid accidental rejects.
            if (string.IsNullOrWhiteSpace(reason))
            {
                var item = await this.repo.GetByIdAsync(id);
                if (item is null)
                {
                    return this.NotFound();
                }

                this.ModelState.AddModelError("reason", "A rejection reason is required.");
                return this.View("Show", item);
            }

            await this.repo.RejectAsync(id, reason);
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/123/delete
        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await this.repo.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Pending));
        }
    }
}