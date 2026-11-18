using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Writing, editing and deleting reviews and replies as a signed-in member. Everything a
    /// member saves waits for an admin in the normal review moderation pages.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.Member)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MemberReviewsController : BaseController
    {
        public const string PendingMessage =
            "Thanks! Your post was saved and is waiting for a moderator. It appears on the listing once it is approved.";

        private readonly MemberAccountService accounts;
        private readonly IMemberReviewService reviews;
        private readonly IDirectoryEntryRepository entries;

        public MemberReviewsController(
            MemberAccountService accounts,
            IMemberReviewService reviews,
            IDirectoryEntryRepository entries,
            IMemoryCache cache)
            : base(cache)
        {
            this.accounts = accounts;
            this.reviews = reviews;
            this.entries = entries;
        }

        [HttpGet("members/reviews")]
        public async Task<IActionResult> Mine(CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            return this.View(await this.reviews.ListOwnAsync(user, ct));
        }

        [HttpGet("members/reviews/write/{entryKey}")]
        public async Task<IActionResult> Write(string entryKey, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var entry = await this.FindOpenEntryAsync(entryKey);
            if (entry == null)
            {
                return this.NotFound();
            }

            var vm = await this.BuildWritePageAsync(user, entry, null, ct);
            if (!vm.IsEdit && await DirectoryManager.Web.Helpers.ReviewerKeyRules.KeyHasPgpReviewAsync(
                    this.HttpContext.RequestServices.GetRequiredService<DirectoryManager.Data.DbContextInfo.ApplicationDbContext>(),
                    user.PgpFingerprint,
                    entry.DirectoryEntryId,
                    ct))
            {
                // Said up front (enforced again when saving).
                this.ModelState.AddModelError(string.Empty, DirectoryManager.Web.Helpers.ReviewerKeyRules.MemberAlreadyReviewedWithKeyMessage);
            }

            await this.AddBlockedNoticeAsync(user, ct);
            return this.View(vm);
        }

        [HttpPost("members/reviews/write/{entryKey}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WritePost(string entryKey, [Bind(Prefix = "Input")] MemberReviewInputModel input, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var entry = await this.FindOpenEntryAsync(entryKey);
            if (entry == null)
            {
                return this.NotFound();
            }

            var result = await this.reviews.SaveReviewAsync(user, entry, input, ct);
            if (result.NotAllowed)
            {
                return this.NotFound();
            }

            if (!result.Succeeded)
            {
                foreach (var (field, message) in result.Errors)
                {
                    this.ModelState.AddModelError(field.Length == 0 ? string.Empty : "Input." + field, message);
                }

                return this.View(nameof(this.Write), await this.BuildWritePageAsync(user, entry, input, ct));
            }

            this.ClearCachedItems();
            if (result.WasPublished)
            {
                // The published version is now back in moderation: refresh the public listing page.
                await this.StaticPublish().EnqueueForEntryAsync(entry.DirectoryEntryId, "member review edited (back to moderation)", null, ct);
            }

            this.TempData["MemberMessage"] = result.WasEdit
                ? "Your review was updated. The new version waits for a moderator before it appears."
                : PendingMessage;
            return this.LocalRedirect(MemberAuth.MyReviewsPath);
        }

        [HttpGet("members/replies/new/{reviewId:int}")]
        public async Task<IActionResult> Reply(int reviewId, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var vm = await this.BuildReplyPageAsync(user, reviewId, null);
            if (vm == null)
            {
                return this.NotFound();
            }

            await this.AddBlockedNoticeAsync(user, ct);
            return this.View(vm);
        }

        [HttpPost("members/replies/new/{reviewId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyPost(int reviewId, [Bind(Prefix = "Input")] MemberReplyInputModel input, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var result = await this.reviews.SaveReplyAsync(user, reviewId, input, ct);
            if (result.NotAllowed)
            {
                return this.NotFound();
            }

            if (!result.Succeeded)
            {
                foreach (var (field, message) in result.Errors)
                {
                    this.ModelState.AddModelError(field.Length == 0 ? string.Empty : "Input." + field, message);
                }

                var vm = await this.BuildReplyPageAsync(user, reviewId, input);
                return vm == null ? this.NotFound() : this.View(nameof(this.Reply), vm);
            }

            this.TempData["MemberMessage"] = PendingMessage;
            return this.LocalRedirect(MemberAuth.MyReviewsPath);
        }

        [HttpPost("members/reviews/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(int id, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var entryId = await this.reviews.DeleteOwnReviewAsync(user.Id, id, ct);
            if (entryId == null)
            {
                return this.NotFound();
            }

            this.ClearCachedItems();
            await this.StaticPublish().EnqueueForEntryAsync(entryId.Value, "review deleted by its member author", null, ct);
            this.TempData["MemberMessage"] = "Your review was deleted.";
            return this.LocalRedirect(MemberAuth.MyReviewsPath);
        }

        [HttpPost("members/replies/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(int id, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var reviewId = await this.reviews.DeleteOwnReplyAsync(user.Id, id, ct);
            if (reviewId == null)
            {
                return this.NotFound();
            }

            this.ClearCachedItems();
            await this.StaticPublish().EnqueueForReviewAsync(reviewId.Value, "reply deleted by its member author", ct);
            this.TempData["MemberMessage"] = "Your reply was deleted.";
            return this.LocalRedirect(MemberAuth.MyReviewsPath);
        }

        // An admin posting block is said up front (enforced again when saving).
        private async Task AddBlockedNoticeAsync(ApplicationUser user, CancellationToken ct)
        {
            var db = this.HttpContext.RequestServices.GetRequiredService<DirectoryManager.Data.DbContextInfo.ApplicationDbContext>();
            if (await DirectoryManager.Web.Helpers.ReviewerBlocks.IsMemberBlockedAsync(db, user, ct))
            {
                this.ModelState.AddModelError(string.Empty, DirectoryManager.Web.Helpers.ReviewerBlocks.MemberBlockedMessage);
            }
        }

        private async Task<DirectoryEntry?> FindOpenEntryAsync(string? entryKey)
        {
            var key = (entryKey ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0 || key.Length > 200)
            {
                return null;
            }

            var entry = await this.entries.GetByKey(key);
            return entry == null || entry.ReviewsDisabled || entry.DirectoryStatus == DirectoryStatus.Removed ? null : entry;
        }

        private async Task<MemberReviewPageViewModel> BuildWritePageAsync(ApplicationUser user, DirectoryEntry entry, MemberReviewInputModel? posted, CancellationToken ct)
        {
            var (requireProof, requireDetails) = await this.reviews.GetVerificationRequirementsAsync(entry, ct);
            var existing = await this.reviews.GetOwnReviewAsync(user.Id, entry.DirectoryEntryId, ct);

            return new MemberReviewPageViewModel
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryName = entry.Name,
                Username = user.UserName ?? string.Empty,
                RequireOrderProof = requireProof,
                RequireDetails = requireDetails,
                ShowSwapFacts = await this.reviews.AcceptsSwapFactsAsync(entry, ct),
                IsEdit = existing != null,
                CurrentStatus = existing?.ModerationStatus,
                RejectionReason = existing?.RejectionReason,
                Input = posted ?? new MemberReviewInputModel
                {
                    Rating = existing?.Rating,
                    Body = existing?.Body,
                    OrderProof = existing?.OrderUrl,
                    OrderProofContext = existing?.OrderProofContext,
                    ProcessingMinutes = existing?.ProcessingMinutes,
                    Confirmations = existing?.Confirmations,
                },
            };
        }

        private async Task<MemberReplyPageViewModel?> BuildReplyPageAsync(ApplicationUser user, int reviewId, MemberReplyInputModel? posted)
        {
            var repo = this.HttpContext.RequestServices.GetRequiredService<IDirectoryEntryReviewRepository>();
            var review = await repo.GetByIdAsync(reviewId);
            if (review == null || review.ModerationStatus != ReviewModerationStatus.Approved)
            {
                return null;
            }

            var entry = review.DirectoryEntry ?? await this.entries.GetByIdAsync(review.DirectoryEntryId);
            if (entry == null || entry.ReviewsDisabled || entry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return null;
            }

            var body = review.Body ?? string.Empty;
            return new MemberReplyPageViewModel
            {
                DirectoryEntryReviewId = reviewId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryName = entry.Name,
                ReviewExcerpt = body.Length > 400 ? body[..400] + "…" : body,
                Username = user.UserName ?? string.Empty,
                Input = posted ?? new MemberReplyInputModel(),
            };
        }
    }
}
