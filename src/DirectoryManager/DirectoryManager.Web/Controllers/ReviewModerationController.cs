using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/reviews")]
    public class ReviewModerationController : BaseController
    {
        private readonly IDirectoryEntryReviewRepository repo;
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;

        public ReviewModerationController(
             IDirectoryEntryReviewRepository repo,
             IDirectoryEntryReviewCommentRepository commentRepo,
             ITrafficLogRepository trafficLogRepository,
             IUserAgentCacheService userAgentCacheService,
             IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.repo = repo;
            this.commentRepo = commentRepo;
        }

        // Default route = Pending queue, but everything lives on the combined All page.
        // GET /admin/reviews
        [HttpGet("")]
        public IActionResult Pending(int page = 1, int pageSize = 50)
        {
            return this.RedirectToAction(nameof(this.All), new
            {
                status = ReviewModerationStatus.Pending,
                reviewPage = page,
                reviewPageSize = pageSize,
                replyPage = page,
                replyPageSize = pageSize
            });
        }

        // GET /admin/reviews/all?status=Approved|Rejected|Pending (optional)
        // Supports paging each table independently:
        //   reviewPage, reviewPageSize, replyPage, replyPageSize
        [HttpGet("all")]
        public async Task<IActionResult> All(
            ReviewModerationStatus? status = null,
            int reviewPage = 1,
            int reviewPageSize = 50,
            int replyPage = 1,
            int replyPageSize = 50,
            CancellationToken ct = default)
        {
            IReadOnlyList<DirectoryEntryReview> reviews;
            int reviewsTotal;

            if (status.HasValue)
            {
                reviews = await this.repo.ListByStatusAsync(status.Value, reviewPage, reviewPageSize);
                reviewsTotal = await this.repo.CountByStatusAsync(status.Value);
            }
            else
            {
                reviews = await this.repo.ListAsync(reviewPage, reviewPageSize);
                reviewsTotal = await this.repo.CountAsync();
            }

            IReadOnlyList<DirectoryEntryReviewComment> replies;
            int repliesTotal;

            if (status.HasValue)
            {
                replies = await this.commentRepo.ListByStatusAsync(status.Value, replyPage, replyPageSize, ct);
                repliesTotal = await this.commentRepo.CountByStatusAsync(status.Value, ct);
            }
            else
            {
                // NOTE: requires these methods in comment repo:
                //   Task<List<DirectoryEntryReviewComment>> ListAsync(int page, int pageSize, CancellationToken ct)
                //   Task<int> CountAsync(CancellationToken ct)
                replies = await this.commentRepo.ListAsync(replyPage, replyPageSize, ct);
                repliesTotal = await this.commentRepo.CountAsync(ct);
            }

            var vm = new ReviewModerationDashboardViewModel
            {
                Status = status,

                Reviews = reviews,
                ReviewsTotal = reviewsTotal,
                ReviewsPage = reviewPage,
                ReviewsPageSize = reviewPageSize,

                Replies = replies,
                RepliesTotal = repliesTotal,
                RepliesPage = replyPage,
                RepliesPageSize = replyPageSize
            };

            return this.View("All", vm);
        }

        // -------- Reviews --------

        // GET /admin/reviews/123
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Show(int id, CancellationToken ct = default)
        {
            var item = await this.repo.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View("Show", item);
        }

        // POST /admin/reviews/123/approve
        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, CancellationToken ct = default)
        {
            await this.repo.ApproveAsync(id, ct);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/123/reject
        [HttpPost("{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, [FromForm] string? reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                var item = await this.repo.GetByIdAsync(id, ct);
                if (item is null)
                {
                    return this.NotFound();
                }

                this.ModelState.AddModelError("reason", "A rejection reason is required.");
                return this.View("Show", item);
            }

            await this.repo.RejectAsync(id, reason, ct);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/123/delete
        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            await this.repo.DeleteAsync(id, ct);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Pending));
        }

        // -------- Comments / Replies (DirectoryEntryReviewComment) --------

        // GET /admin/reviews/reply/123
        // (Single canonical route; don’t duplicate ShowReply endpoints)
        [HttpGet("reply/{id:int}")]
        public async Task<IActionResult> ShowReply(int id, CancellationToken ct = default)
        {
            var item = await this.commentRepo.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View("ShowReply", item);
        }

        // POST /admin/reviews/reply/123/approve
        [HttpPost("reply/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveReply(int id, CancellationToken ct = default)
        {
            await this.commentRepo.ApproveAsync(id, ct);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/reply/123/reject
        [HttpPost("reply/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReply(int id, [FromForm] string? reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                var item = await this.commentRepo.GetByIdAsync(id, ct);
                if (item is null)
                {
                    return this.NotFound();
                }

                this.ModelState.AddModelError("reason", "A rejection reason is required.");
                return this.View("ShowReply", item);
            }

            await this.commentRepo.RejectAsync(id, reason, ct);
            return this.RedirectToAction(nameof(this.Pending));
        }

        // POST /admin/reviews/reply/123/delete
        [HttpPost("reply/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int id, CancellationToken ct = default)
        {
            await this.commentRepo.DeleteAsync(id, ct);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Pending));
        }
    }
}