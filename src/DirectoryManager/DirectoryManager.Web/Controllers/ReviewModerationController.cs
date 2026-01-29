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
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;

        public ReviewModerationController(
            IDirectoryEntryReviewRepository repo,
            IDirectoryEntryReviewCommentRepository commentRepo)
        {
            this.repo = repo;
            this.commentRepo = commentRepo;
        }

        [HttpGet("")]
        public async Task<IActionResult> Pending(int page = 1, int pageSize = 50)
        {
            var pendingReviews = await this.repo.ListByStatusAsync(ReviewModerationStatus.Pending, page, pageSize);
            var pendingReviewsTotal = await this.repo.CountByStatusAsync(ReviewModerationStatus.Pending);

            var pendingReplies = await this.commentRepo.ListByStatusAsync(ReviewModerationStatus.Pending, page, pageSize);
            var pendingRepliesTotal = await this.commentRepo.CountByStatusAsync(ReviewModerationStatus.Pending);

            this.ViewBag.ReviewTotal = pendingReviewsTotal;
            this.ViewBag.ReplyTotal = pendingRepliesTotal;

            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;

            var vm = new DirectoryManager.Web.Models.ReviewModerationQueueViewModel
            {
                PendingReviews = pendingReviews,
                PendingReplies = pendingReplies
            };

            return this.View("Pending", vm);
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

        // GET /admin/reviews/replies/123
        [HttpGet("replies/{id:int}")]
        public async Task<IActionResult> ShowReply(int id, CancellationToken ct)
        {
            var item = await this.commentRepo.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View("ShowReply", item); // ✅ IMPORTANT
        }

        // GET /admin/reviews/reply/123
        [HttpGet("reply/{id:int}")]
        public async Task<IActionResult> ShowReply(int id)
        {
            var item = await this.commentRepo.GetByIdAsync(id);
            if (item is null) return this.NotFound();
            return this.View("ShowReply", item);
        }

        // POST /admin/reviews/reply/123/approve
        [HttpPost("reply/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReply(int id)
        {
            await this.commentRepo.ApproveAsync(id);
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/reply/123/reject
        [HttpPost("reply/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReply(int id, [FromForm] string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                var item = await this.commentRepo.GetByIdAsync(id);
                if (item is null) return this.NotFound();

                this.ModelState.AddModelError("reason", "A rejection reason is required.");
                return this.View("ShowReply", item);
            }

            await this.commentRepo.RejectAsync(id, reason);
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/reply/123/delete
        [HttpPost("reply/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int id)
        {
            await this.commentRepo.DeleteAsync(id);
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