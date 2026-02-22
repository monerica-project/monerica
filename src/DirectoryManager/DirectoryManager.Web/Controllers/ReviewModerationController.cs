using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/reviews")]
    public class ReviewModerationController : BaseController
    {
        private readonly IDirectoryEntryReviewRepository repo;
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;
        private readonly IReviewTagRepository reviewTagRepository;
        private readonly IDirectoryEntryReviewTagRepository reviewTagLinkRepository;

        public ReviewModerationController(
             IDirectoryEntryReviewRepository repo,
             IDirectoryEntryReviewCommentRepository commentRepo,
             ITrafficLogRepository trafficLogRepository,
             IUserAgentCacheService userAgentCacheService,
             IReviewTagRepository reviewTagRepository,
             IDirectoryEntryReviewTagRepository reviewTagLinkRepository,
             IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.repo = repo;
            this.commentRepo = commentRepo;
            this.reviewTagRepository = reviewTagRepository;
            this.reviewTagLinkRepository = reviewTagLinkRepository;
        }

        // Default route = Pending queue
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
                reviews = await this.repo.ListByStatusAsync(status.Value, reviewPage, reviewPageSize, ct);
                reviewsTotal = await this.repo.CountByStatusAsync(status.Value, ct);
            }
            else
            {
                reviews = await this.repo.ListAsync(reviewPage, reviewPageSize, ct);
                reviewsTotal = await this.repo.CountAsync(ct);
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
            // Need tags for rendering checkboxes
            var item = await this.repo.Query()
                .Include(r => r.ReviewTags)
                    .ThenInclude(rt => rt.ReviewTag)
                .FirstOrDefaultAsync(r => r.DirectoryEntryReviewId == id, ct);

            if (item is null)
            {
                return this.NotFound();
            }

            var allTags = await this.reviewTagRepository.ListAllAsync(ct);

            var vm = new ReviewModerationReviewViewModel
            {
                Review = item,
                OrderProof = item.OrderUrl ?? item.OrderId,
                SelectedTagIds = item.ReviewTags.Select(x => x.ReviewTagId).ToList(),
                AllTags = allTags.Select(t => new ReviewModerationReviewViewModel.TagOption
                {
                    Id = t.ReviewTagId,
                    Name = t.Name,
                    IsEnabled = t.IsEnabled
                }).ToList()
            };

            return this.View("Show", vm);
        }

        // POST /admin/reviews/123/update
        // Save OrderProof + Tags without approving/rejecting
        [HttpPost("{id:int}/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            [FromForm] string? orderProof,
            [FromForm] int[]? selectedTagIds,
            CancellationToken ct = default)
        {
            var review = await this.repo.GetByIdAsync(id, ct);
            if (review is null)
            {
                return this.NotFound();
            }

            ApplyOrderProof(review, orderProof);

            // If proof is present and review was Approved, force back to Pending
            var hasProof = !string.IsNullOrWhiteSpace(review.OrderId) || !string.IsNullOrWhiteSpace(review.OrderUrl);
            if (hasProof && review.ModerationStatus == ReviewModerationStatus.Approved)
            {
                review.ModerationStatus = ReviewModerationStatus.Pending;
            }

            await this.repo.UpdateAsync(review, ct);

            var tagIds = (selectedTagIds ?? Array.Empty<int>()).Distinct().ToArray();
            await this.reviewTagLinkRepository.SetTagsForReviewAsync(
                review.DirectoryEntryReviewId,
                tagIds,
                userId: this.User?.Identity?.Name ?? "admin",
                ct);

            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Show), new { id });
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
                // Re-render show with vm again
                return await this.Show(id, ct);
            }

            await this.repo.RejectAsync(id, reason.Trim(), ct);
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

        // -------- Comments / Replies --------

        // GET /admin/reviews/reply/123
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

            await this.commentRepo.RejectAsync(id, reason.Trim(), ct);
            this.ClearCachedItems();
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

        // -------------------------
        // Helpers
        // -------------------------
        private static void ApplyOrderProof(DirectoryEntryReview review, string? orderProof)
        {
            var s = (orderProof ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(s))
            {
                review.OrderId = null;
                review.OrderUrl = null;
                return;
            }

            if (Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                review.OrderUrl = s;
                review.OrderId = null;
            }
            else
            {
                review.OrderId = s;
                review.OrderUrl = null;
            }
        }
    }
}
