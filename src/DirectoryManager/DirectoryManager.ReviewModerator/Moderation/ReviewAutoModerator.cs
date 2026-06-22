using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.ReviewModerator.Abstractions;
using DirectoryManager.ReviewModerator.Fetching;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.ReviewModerator.Moderation
{
    /// <summary>
    /// Evaluates a single pending, order-URL-bearing review and applies the outcome.
    ///
    /// Hard rule: a review is only auto-APPROVED when its order proof is in a terminal
    /// SUCCESS state (Completed/Finished). Terminal FAILURE => auto-Rejected. Anything
    /// uncertain — unknown status, still in progress past the retry window, unreachable,
    /// no parser, locked behind missing context, possible replay — is FLAGGED for a human.
    /// The worker never approves on a guess.
    /// </summary>
    public sealed class ReviewAutoModerator
    {
        private const string ValidOrderSlug = "valid-order";

        private readonly IDirectoryEntryReviewRepository reviewRepo;
        private readonly IReviewTagRepository tagRepo;
        private readonly IDirectoryEntryReviewTagRepository tagLinkRepo;
        private readonly OrderProofParserRegistry registry;
        private readonly IOrderProofFetcher fetcher;
        private readonly IPriceLookupService prices;
        private readonly int maxAttempts;

        public ReviewAutoModerator(
            IDirectoryEntryReviewRepository reviewRepo,
            IReviewTagRepository tagRepo,
            IDirectoryEntryReviewTagRepository tagLinkRepo,
            OrderProofParserRegistry registry,
            IOrderProofFetcher fetcher,
            IPriceLookupService prices,
            int maxAttempts)
        {
            this.reviewRepo = reviewRepo;
            this.tagRepo = tagRepo;
            this.tagLinkRepo = tagLinkRepo;
            this.registry = registry;
            this.fetcher = fetcher;
            this.prices = prices;
            this.maxAttempts = maxAttempts;
        }

        public async Task<AutoModerationResult> ProcessAsync(int reviewId, CancellationToken ct)
        {
            var review = await this.reviewRepo.GetWithTagsByIdAsync(reviewId, ct);
            if (review is null || review.ModerationStatus != ReviewModerationStatus.Pending)
            {
                return AutoModerationResult.None;
            }

            review.AutoModerationAttemptCount += 1;
            review.LastAutoModerationAttemptUtc = DateTime.UtcNow;

            var parser = this.registry.Resolve(review.OrderUrl);
            if (parser is null)
            {
                return await this.FlagAsync(review, $"No order-proof parser for domain of '{review.OrderUrl}'.", ct);
            }

            if (parser.RequiresUnlockContext && string.IsNullOrWhiteSpace(review.OrderProofContext))
            {
                return await this.FlagAsync(review, "Order details require supporting content (e.g. wallet address) that was not supplied.", ct);
            }

            var lookup = parser.BuildLookupUri(review.OrderUrl!, review.OrderProofContext);
            if (lookup is null)
            {
                return await this.FlagAsync(review, "Order URL is not in the expected format for this exchange.", ct);
            }

            var fetch = await this.fetcher.GetAsync(lookup, ct);
            if (!fetch.Success)
            {
                // Transient: retry on later passes, flag once the window is exhausted.
                return await this.RetryOrFlagAsync(review, $"Proof URL unreachable ({fetch.Error}).", ct);
            }

            var result = parser.Parse(fetch.Body, fetch.ContentType);

            // Terminal failure => the order was never completed (refunded/expired/etc.).
            if (result.IsTerminalFailure)
            {
                return await this.RejectAsync(review, $"Order is {result.Status} on the exchange — not a completed swap.", ct);
            }

            // Non-terminal (awaiting deposit / confirming / exchanging / sending): the common
            // subversion pattern is submitting a review for an unfinished order. Retry within
            // the window, then flag — never auto-reject (it may simply complete later).
            if (!result.IsTerminalSuccess && result.Status != OrderProofStatus.NeedsUnlockContext)
            {
                return await this.RetryOrFlagAsync(review, $"Order not completed (status: {result.Status}).", ct);
            }

            if (result.Status == OrderProofStatus.NeedsUnlockContext)
            {
                return await this.FlagAsync(review, "Order details are locked behind supporting content.", ct);
            }

            // ---- Terminal success path ----

            // Replay guard: another review already used this order proof.
            if (await this.IsDuplicateProofAsync(review, ct))
            {
                return await this.FlagAsync(review, "This order URL is already associated with another review (possible replay).", ct);
            }

            // Best-effort USD valuation of the deposit leg (completion is the hard gate; the
            // money-band tag is best-effort and simply skipped while pricing is unavailable).
            decimal? usd = null;
            string priceNote;
            if (!string.IsNullOrWhiteSpace(result.SentAsset) && result.SentAmount is > 0)
            {
                var price = await this.prices.GetUsdValueAsync(result.SentAsset!, result.SentAmount!.Value, result.CompletedAtUtc, ct);
                if (price.Available)
                {
                    usd = price.UsdValue;
                }

                priceNote = price.Available ? $"usd≈{usd:0.00}" : $"usd unavailable ({price.Note})";
            }
            else
            {
                priceNote = "sent leg not parsed";
            }

            var tagIds = await this.BuildApprovedTagIdsAsync(review, usd, ct);

            review.ModerationStatus = ReviewModerationStatus.Approved;
            review.AutoModerationResult = AutoModerationResult.AutoApproved;
            review.VerifiedOrderUsdValue = usd;
            review.AutoModeratedAtUtc = DateTime.UtcNow;
            review.AutoModerationReason =
                $"Completed swap {result.SentAmount} {result.SentAsset} → {result.ReceivedAmount} {result.ReceivedAsset}; {priceNote}.";

            await this.reviewRepo.UpdateAsync(review, ct);
            await this.tagLinkRepo.SetTagsForReviewAsync(review.DirectoryEntryReviewId, tagIds, "auto-moderator", ct);
            return AutoModerationResult.AutoApproved;
        }

        private async Task<List<int>> BuildApprovedTagIdsAsync(DirectoryEntryReview review, decimal? usd, CancellationToken ct)
        {
            // Preserve any tags a human already attached; add valid-order (+ matching band).
            var ids = review.ReviewTags.Select(t => t.ReviewTagId).ToHashSet();

            var allEnabled = await this.tagRepo.ListEnabledAsync(ct);

            var validOrder = allEnabled.FirstOrDefault(t =>
                string.Equals(t.Slug, ValidOrderSlug, StringComparison.OrdinalIgnoreCase));
            if (validOrder is not null)
            {
                ids.Add(validOrder.ReviewTagId);
            }

            if (usd.HasValue)
            {
                var bands = allEnabled.Where(t => t.IsMoneyBand && t.Matches(usd.Value)).ToList();
                if (bands.Count == 1)
                {
                    ids.Add(bands[0].ReviewTagId);
                }

                // 0 or >1 matches => ambiguous band config; leave money untagged for a human.
            }

            return ids.ToList();
        }

        private async Task<bool> IsDuplicateProofAsync(DirectoryEntryReview review, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(review.OrderUrl))
            {
                return false;
            }

            return await this.reviewRepo.Query()
                .AnyAsync(
                    r => r.DirectoryEntryReviewId != review.DirectoryEntryReviewId
                         && r.OrderUrl == review.OrderUrl
                         && (r.ModerationStatus == ReviewModerationStatus.Approved
                             || r.AutoModerationResult == AutoModerationResult.AutoApproved),
                    ct);
        }

        private async Task<AutoModerationResult> RetryOrFlagAsync(DirectoryEntryReview review, string reason, CancellationToken ct)
        {
            if (review.AutoModerationAttemptCount >= this.maxAttempts)
            {
                return await this.FlagAsync(review, $"{reason} Still unresolved after {this.maxAttempts} checks.", ct);
            }

            // Leave Pending; just persist the bumped attempt counters for back-off.
            review.AutoModerationReason = $"{reason} Will retry ({review.AutoModerationAttemptCount}/{this.maxAttempts}).";
            await this.reviewRepo.UpdateAsync(review, ct);
            return AutoModerationResult.None;
        }

        private async Task<AutoModerationResult> RejectAsync(DirectoryEntryReview review, string reason, CancellationToken ct)
        {
            review.ModerationStatus = ReviewModerationStatus.Rejected;
            review.RejectionReason = reason;
            review.AutoModerationResult = AutoModerationResult.AutoRejected;
            review.AutoModerationReason = reason;
            review.AutoModeratedAtUtc = DateTime.UtcNow;
            await this.reviewRepo.UpdateAsync(review, ct);
            return AutoModerationResult.AutoRejected;
        }

        private async Task<AutoModerationResult> FlagAsync(DirectoryEntryReview review, string reason, CancellationToken ct)
        {
            review.ModerationStatus = ReviewModerationStatus.Flagged;
            review.AutoModerationResult = AutoModerationResult.Flagged;
            review.AutoModerationReason = reason;
            review.AutoModeratedAtUtc = DateTime.UtcNow;
            await this.reviewRepo.UpdateAsync(review, ct);
            return AutoModerationResult.Flagged;
        }
    }
}
