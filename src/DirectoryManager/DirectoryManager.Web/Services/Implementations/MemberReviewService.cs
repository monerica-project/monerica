using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Member reviews and replies. The content rules are the ones the PGP review flow uses
    /// (shared moderation service, order-proof URL rule, listing-domain rule, required proof /
    /// details per listing, duplicate-text guard, reviews closed / removed listings), with one
    /// difference: nothing a member writes is ever auto-approved.
    /// Abuse limits are per account (no IP addresses): new reviews, edits and replies per day, and
    /// a cap on how many of the member's posts may wait for moderation at once.
    /// </summary>
    public sealed class MemberReviewService : IMemberReviewService
    {
        public const int MaxNewReviewsPerDay = 5;
        public const int MaxReviewEditsPerDay = 10;
        public const int MaxRepliesPerDay = 20;
        public const int MaxPendingPosts = 10;

        public const string MemberUserMarker = "member";

        private readonly ApplicationDbContext db;
        private readonly IUserContentModerationService moderation;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IDirectoryEntryReviewCommentRepository commentRepository;
        private readonly TimeProvider clock;

        public MemberReviewService(
            ApplicationDbContext db,
            IUserContentModerationService moderation,
            IDirectoryEntryReviewRepository reviewRepository,
            IDirectoryEntryReviewCommentRepository commentRepository,
            TimeProvider? clock = null)
        {
            this.db = db;
            this.moderation = moderation;
            this.reviewRepository = reviewRepository;
            this.commentRepository = commentRepository;
            this.clock = clock ?? TimeProvider.System;
        }

        private DateTime NowUtc => this.clock.GetUtcNow().UtcDateTime;

        public Task<DirectoryEntryReview?> GetOwnReviewAsync(string userId, int directoryEntryId, CancellationToken ct = default) =>
            this.db.DirectoryEntryReviews
                .Where(r => r.AuthorUserId == userId && r.DirectoryEntryId == directoryEntryId)
                .OrderBy(r => r.DirectoryEntryReviewId)
                .FirstOrDefaultAsync(ct);

        public async Task<(bool RequireOrderProof, bool RequireDetails)> GetVerificationRequirementsAsync(DirectoryEntry entry, CancellationToken ct = default)
        {
            var subRequires = await this.db.Subcategories.AsNoTracking()
                .Where(s => s.SubCategoryId == entry.SubCategoryId)
                .Select(s => s.RequireReviewVerification)
                .FirstOrDefaultAsync(ct);
            return (subRequires, entry.RequireReviewVerificationDetails);
        }

        public async Task<MemberSaveResult> SaveReviewAsync(ApplicationUser member, DirectoryEntry? entry, MemberReviewInputModel input, CancellationToken ct = default)
        {
            if (entry == null || entry.ReviewsDisabled || entry.DirectoryStatus == DirectoryStatus.Removed || !MemberAccountService.IsActive(member))
            {
                return MemberSaveResult.Denied();
            }

            var result = new MemberSaveResult();

            // Admin posting block (account, or its linked PGP key): no new reviews and no edits.
            if (await ReviewerBlocks.IsMemberBlockedAsync(this.db, member, ct))
            {
                return result.Error(string.Empty, ReviewerBlocks.MemberBlockedMessage);
            }

            var (requireProof, requireDetails) = await this.GetVerificationRequirementsAsync(entry, ct);
            var existing = await this.GetOwnReviewAsync(member.Id, entry.DirectoryEntryId, ct);

            // One review per reviewer per listing across both paths: a member whose linked key
            // already reviewed this listing through the PGP flow can't add a second review here.
            if (existing == null && await ReviewerKeyRules.KeyHasPgpReviewAsync(this.db, member.PgpFingerprint, entry.DirectoryEntryId, ct))
            {
                return result.Error(string.Empty, ReviewerKeyRules.MemberAlreadyReviewedWithKeyMessage);
            }

            // Per-account limits (checked before any content work).
            var since = this.NowUtc.AddDays(-1);
            if (existing == null)
            {
                var newToday = await this.db.DirectoryEntryReviews.CountAsync(r => r.AuthorUserId == member.Id && r.CreateDate > since, ct);
                if (newToday >= MaxNewReviewsPerDay)
                {
                    return result.Error(string.Empty, $"You can post up to {MaxNewReviewsPerDay} new reviews a day. Please try again tomorrow.");
                }
            }
            else
            {
                var editsToday = await this.db.DirectoryEntryReviews.CountAsync(r => r.AuthorUserId == member.Id && r.UpdateDate != null && r.UpdateDate > since, ct);
                if (editsToday >= MaxReviewEditsPerDay)
                {
                    return result.Error(string.Empty, $"You can edit reviews up to {MaxReviewEditsPerDay} times a day. Please try again tomorrow.");
                }
            }

            if (await this.PendingCountAsync(member.Id, existing?.DirectoryEntryReviewId, ct) >= MaxPendingPosts)
            {
                return result.Error(string.Empty, $"You already have {MaxPendingPosts} reviews or replies waiting for a moderator. Please wait until they are checked.");
            }

            if (input.Rating is not byte rating || rating < 1 || rating > 5)
            {
                result.Error(nameof(input.Rating), "Please choose a rating from 1 to 5.");
                rating = 0;
            }

            var body = UnicodeSanitizer.CleanMultiLine(input.Body ?? string.Empty).Trim();
            if (body.Length > MemberReviewInputModel.MaxBodyLength)
            {
                result.Error(nameof(input.Body), $"Your review is too long. Please keep it under {MemberReviewInputModel.MaxBodyLength:N0} characters.");
            }
            else
            {
                var mod = await this.moderation.EvaluateReviewAsync(body, ct);
                if (!mod.IsValid)
                {
                    result.Error(nameof(input.Body), mod.ValidationErrorMessage ?? "Your review couldn't be accepted. Please revise it and try again.");
                }
                else if (await this.reviewRepository.ExistsByBodyAsync(body, existing?.DirectoryEntryReviewId, ct))
                {
                    result.Error(nameof(input.Body), "A review with this exact message has already been submitted. Please write something original.");
                }
            }

            // Order proof: same rules as the PGP review form.
            string? proofUrl = null;
            if (requireProof && string.IsNullOrWhiteSpace(input.OrderProof))
            {
                result.Error(nameof(input.OrderProof), "An order proof URL is required for reviews in this category (for example https://shop.example.com/orders/123).");
            }
            else if (!DirectoryEntryReviewsController.TryNormalizeOrderProofUrl(input.OrderProof, out proofUrl))
            {
                result.Error(nameof(input.OrderProof), "Order proof must be a URL (for example https://shop.example.com/orders/123). Leave it blank if you don't have one.");
            }
            else if (!string.IsNullOrWhiteSpace(proofUrl))
            {
                var links = new[] { entry.Link, entry.Link2, entry.Link3 }.Where(l => !string.IsNullOrWhiteSpace(l));
                if (!links.Any(l => UrlHelper.HostsRelated(proofUrl, l)))
                {
                    var host = UrlHelper.TryGetHost(entry.Link);
                    result.Error(
                        nameof(input.OrderProof),
                        "The order proof URL must be on the reviewed listing's own site" +
                        (host is { } h ? $" ({h})" : string.Empty) +
                        " — its own domain, .onion, or I2P address.");
                }
            }

            var details = DirectoryEntryReviewsController.NormalizeOrderProofContext(input.OrderProofContext);
            if (requireProof || requireDetails)
            {
                // Swap listings: the reviewer's own order link and the destination wallet address.
                if (!string.IsNullOrWhiteSpace(proofUrl) && !ReviewOrderProofRules.LooksLikeOwnOrderLink(proofUrl))
                {
                    result.Error(nameof(input.OrderProof), ReviewOrderProofRules.GenericLinkMessage);
                }

                if (!ReviewOrderProofRules.ContainsWalletAddress(details))
                {
                    result.Error(nameof(input.OrderProofContext), ReviewOrderProofRules.AddressMissingMessage);
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            var now = this.NowUtc;

            if (existing != null)
            {
                // An edit always goes back to moderation, even when the review was published.
                result.WasPublished = existing.ModerationStatus == ReviewModerationStatus.Approved;
                existing.Rating = rating;
                existing.Body = body;
                existing.OrderUrl = proofUrl;
                existing.OrderId = null;
                existing.OrderProofContext = details;
                existing.ModerationStatus = ReviewModerationStatus.Pending;
                existing.RejectionReason = string.Empty;
                existing.AuthorUsername = member.UserName;
                existing.UpdateDate = now;
                existing.UpdatedByUserId = MemberUserMarker;
                await this.db.SaveChangesAsync(ct);
                result.WasEdit = true;
                result.Id = existing.DirectoryEntryReviewId;
                return result;
            }

            var review = new DirectoryEntryReview
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                AuthorFingerprint = MemberAuthor.AuthorKey(member.Id),
                AuthorUserId = member.Id,
                AuthorUsername = member.UserName,
                Rating = rating,
                Body = body,
                OrderUrl = proofUrl,
                OrderProofContext = details,
                ModerationStatus = ReviewModerationStatus.Pending,
                CreateDate = now,
                CreatedByUserId = MemberUserMarker,
            };
            this.db.DirectoryEntryReviews.Add(review);
            await this.db.SaveChangesAsync(ct);
            result.Id = review.DirectoryEntryReviewId;
            return result;
        }

        public async Task<MemberSaveResult> SaveReplyAsync(ApplicationUser member, int directoryEntryReviewId, MemberReplyInputModel input, CancellationToken ct = default)
        {
            var review = await this.db.DirectoryEntryReviews
                .Include(r => r.DirectoryEntry)
                .FirstOrDefaultAsync(r => r.DirectoryEntryReviewId == directoryEntryReviewId, ct);
            if (review == null
                || !MemberAccountService.IsActive(member)
                || review.ModerationStatus != ReviewModerationStatus.Approved
                || review.DirectoryEntry == null
                || review.DirectoryEntry.ReviewsDisabled
                || review.DirectoryEntry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return MemberSaveResult.Denied();
            }

            var result = new MemberSaveResult();
            if (await ReviewerBlocks.IsMemberBlockedAsync(this.db, member, ct))
            {
                return result.Error(string.Empty, ReviewerBlocks.MemberBlockedMessage);
            }

            var since = this.NowUtc.AddDays(-1);
            if (await this.db.DirectoryEntryReviewComments.CountAsync(c => c.AuthorUserId == member.Id && c.CreateDate > since, ct) >= MaxRepliesPerDay)
            {
                return result.Error(string.Empty, $"You can post up to {MaxRepliesPerDay} replies a day. Please try again tomorrow.");
            }

            if (await this.PendingCountAsync(member.Id, null, ct) >= MaxPendingPosts)
            {
                return result.Error(string.Empty, $"You already have {MaxPendingPosts} reviews or replies waiting for a moderator. Please wait until they are checked.");
            }

            // Emojis are not allowed in replies — stripped before moderation, as in the PGP reply flow.
            var body = UnicodeSanitizer.StripEmoji(input.Body ?? string.Empty).Trim();
            if (body.Length > MemberReplyInputModel.MaxBodyLength)
            {
                return result.Error(nameof(input.Body), $"Your reply is too long. Please keep it under {MemberReplyInputModel.MaxBodyLength:N0} characters.");
            }

            var mod = await this.moderation.EvaluateReplyAsync(body, ct);
            if (!mod.IsValid)
            {
                return result.Error(nameof(input.Body), mod.ValidationErrorMessage ?? "Your reply couldn't be accepted. Please revise it and try again.");
            }

            // A nested reply must point at an existing, non-rejected reply on THIS review.
            int? parentId = null;
            if (input.ParentCommentId is int pid && pid > 0)
            {
                var parent = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.DirectoryEntryReviewCommentId == pid, ct);
                parentId = SiteOwnerAdminController.IsValidParentComment(parent, directoryEntryReviewId) ? pid : null;
            }

            var reply = new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = directoryEntryReviewId,
                ParentCommentId = parentId,
                Body = body,
                AuthorFingerprint = MemberAuthor.AuthorKey(member.Id),
                AuthorUserId = member.Id,
                AuthorUsername = member.UserName,
                ModerationStatus = ReviewModerationStatus.Pending,
                CreateDate = this.NowUtc,
                CreatedByUserId = MemberUserMarker,
            };
            this.db.DirectoryEntryReviewComments.Add(reply);
            await this.db.SaveChangesAsync(ct);
            result.Id = reply.DirectoryEntryReviewCommentId;
            return result;
        }

        public async Task<MyMemberReviewsViewModel> ListOwnAsync(ApplicationUser member, CancellationToken ct = default)
        {
            return new MyMemberReviewsViewModel
            {
                Username = member.UserName ?? string.Empty,
                Reviews = await this.db.DirectoryEntryReviews.AsNoTracking()
                    .Include(r => r.DirectoryEntry)
                    .Where(r => r.AuthorUserId == member.Id)
                    .OrderByDescending(r => r.CreateDate)
                    .ToListAsync(ct),
                Replies = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                    .Include(c => c.DirectoryEntryReview!).ThenInclude(r => r.DirectoryEntry)
                    .Where(c => c.AuthorUserId == member.Id)
                    .OrderByDescending(c => c.CreateDate)
                    .ToListAsync(ct),
            };
        }

        public async Task<int?> DeleteOwnReviewAsync(string userId, int directoryEntryReviewId, CancellationToken ct = default)
        {
            var entryId = await this.db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.DirectoryEntryReviewId == directoryEntryReviewId && r.AuthorUserId == userId)
                .Select(r => (int?)r.DirectoryEntryId)
                .FirstOrDefaultAsync(ct);
            if (entryId == null)
            {
                return null;
            }

            // Gone from the site, but an admin-only copy (with its replies) is kept and marked
            // "deleted by author", so the owner can see it happened and restore it.
            await this.reviewRepository.DeleteAsync(directoryEntryReviewId, ct, DirectoryManager.Data.Models.Reviews.DeletedReviewRecord.ByAuthor);
            return entryId;
        }

        public async Task<int?> DeleteOwnReplyAsync(string userId, int directoryEntryReviewCommentId, CancellationToken ct = default)
        {
            var reviewId = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => c.DirectoryEntryReviewCommentId == directoryEntryReviewCommentId && c.AuthorUserId == userId)
                .Select(c => (int?)c.DirectoryEntryReviewId)
                .FirstOrDefaultAsync(ct);
            if (reviewId == null)
            {
                return null;
            }

            await this.commentRepository.DeleteAsync(directoryEntryReviewCommentId, ct, DirectoryManager.Data.Models.Reviews.DeletedReviewRecord.ByAuthor);
            return reviewId;
        }

        private async Task<int> PendingCountAsync(string userId, int? excludeReviewId, CancellationToken ct)
        {
            var reviews = await this.db.DirectoryEntryReviews.CountAsync(
                r => r.AuthorUserId == userId
                     && (r.ModerationStatus == ReviewModerationStatus.Pending || r.ModerationStatus == ReviewModerationStatus.Flagged)
                     && (excludeReviewId == null || r.DirectoryEntryReviewId != excludeReviewId),
                ct);
            var replies = await this.db.DirectoryEntryReviewComments.CountAsync(
                c => c.AuthorUserId == userId
                     && (c.ModerationStatus == ReviewModerationStatus.Pending || c.ModerationStatus == ReviewModerationStatus.Flagged),
                ct);
            return reviews + replies;
        }
    }
}
