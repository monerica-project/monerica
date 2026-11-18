using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Route("raffle")]
    public class RaffleController : BaseController
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> EntriesBeingCreated = new ();

        private const string TokenExpiredMessage = "This raffle link has expired or is invalid. Please check your review confirmation.";

        private readonly IMemoryCache cache;
        private readonly IDirectoryEntryReviewRaffleEntryRepository raffleEntryRepository;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IRaffleRepository raffleRepository;

        public RaffleController(
            IMemoryCache cache,
            IDirectoryEntryReviewRaffleEntryRepository raffleEntryRepository,
            IDirectoryEntryReviewRepository reviewRepository,
            IRaffleRepository raffleRepository)
            : base(cache)
        {
            this.cache = cache;
            this.raffleEntryRepository = raffleEntryRepository;
            this.reviewRepository = reviewRepository;
            this.raffleRepository = raffleRepository;
        }

        // ------------------------------------------------------------------
        // GET /raffle/enter?token=<guid>
        // Shows the address entry form, or redirects to AlreadyEntered / Closed.
        // ------------------------------------------------------------------
        [HttpGet("enter")]
        public async Task<IActionResult> Enter(Guid token, CancellationToken ct = default)
        {
            if (!this.TryGetRaffleToken(token, out var state))
            {
                return this.BadRequest(TokenExpiredMessage);
            }

            // 🚫 No active raffle = no way to enter an address.
            var active = await this.raffleRepository.GetActiveAsync(DateTime.UtcNow, ct);
            if (active is null)
            {
                return this.RedirectToAction(nameof(this.Closed));
            }

            // Is the author already in an active raffle entry?
            var existing = await this.raffleEntryRepository.GetActiveEntryByFingerprintAsync(state.Fingerprint, ct);
            if (existing is not null)
            {
                return this.RedirectToAction(nameof(this.AlreadyEntered));
            }

            this.ViewBag.Token = token;
            this.ViewBag.ReviewId = state.ReviewId;
            this.ViewBag.RaffleName = active.Name;
            this.ViewBag.RaffleEndsUtc = active.EndDate;
            return this.View(new RaffleEnterInputModel());
        }

        // ------------------------------------------------------------------
        // POST /raffle/enter
        // ------------------------------------------------------------------
        [HttpPost("enter")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterPost(Guid token, RaffleEnterInputModel input, CancellationToken ct = default)
        {
            if (!this.TryGetRaffleToken(token, out var state))
            {
                return this.BadRequest(TokenExpiredMessage);
            }

            // 🚫 Re-check: raffle could have ended between render and submit.
            var active = await this.raffleRepository.GetActiveAsync(DateTime.UtcNow, ct);
            if (active is null)
            {
                return this.RedirectToAction(nameof(this.Closed));
            }

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.Token = token;
                this.ViewBag.ReviewId = state.ReviewId;
                this.ViewBag.RaffleName = active.Name;
                this.ViewBag.RaffleEndsUtc = active.EndDate;
                return this.View("Enter", input);
            }

            // One entry per reviewer: parallel posts for the same key stop here, so the
            // check-then-insert below cannot run twice at once.
            var lockKey = (state.Fingerprint ?? string.Empty).ToUpperInvariant();
            if (!EntriesBeingCreated.TryAdd(lockKey, 0))
            {
                return this.RedirectToAction(nameof(this.AlreadyEntered));
            }

            try
            {
                return await this.CreateEntryAsync(token, state.Fingerprint, state.ReviewId, active.RaffleId, input, ct);
            }
            finally
            {
                EntriesBeingCreated.TryRemove(lockKey, out _);
            }
        }

        private async Task<IActionResult> CreateEntryAsync(
            Guid token, string fingerprint, int reviewId, int raffleId, RaffleEnterInputModel input, CancellationToken ct)
        {
            // Re-check: still no active entry for this author?
            var existing = await this.raffleEntryRepository.GetActiveEntryByFingerprintAsync(fingerprint, ct);
            if (existing is not null)
            {
                return this.RedirectToAction(nameof(this.AlreadyEntered));
            }

            // Verify the review still exists and belongs to this fingerprint
            var review = await this.reviewRepository.GetByIdAsync(reviewId, ct);
            if (review is null || !review.AuthorFingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequest("Review not found or fingerprint mismatch.");
            }

            var entry = new DirectoryEntryReviewRaffleEntry
            {
                DirectoryEntryReviewId = reviewId,
                RaffleId = raffleId, // ← ties this entry to the currently-active raffle
                CryptoType = input.CryptoType.Trim().ToUpperInvariant(),
                CryptoAddress = input.CryptoAddress.Trim(),
                Status = RaffleEntryStatus.Pending
            };

            await this.raffleEntryRepository.AddAsync(entry, ct);

            // Burn the token so it can't be used again
            this.cache.Remove(DirectoryEntryReviewsController.RaffleTokenCacheKey(token));

            return this.RedirectToAction(nameof(this.Entered));
        }

        // ------------------------------------------------------------------
        // GET /raffle/entered  — confirmation
        // ------------------------------------------------------------------
        [HttpGet("entered")]
        public IActionResult Entered() => this.View();

        // ------------------------------------------------------------------
        // GET /raffle/already-entered  — blocked: active entry already exists
        // ------------------------------------------------------------------
        [HttpGet("already-entered")]
        public IActionResult AlreadyEntered() => this.View();

        // ------------------------------------------------------------------
        // GET /raffle/closed  — no raffle is currently active
        // ------------------------------------------------------------------
        [HttpGet("closed")]
        public IActionResult Closed() => this.View();

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private bool TryGetRaffleToken(Guid token, out RaffleFlowState state)
        {
            return this.cache.TryGetValue(
                DirectoryEntryReviewsController.RaffleTokenCacheKey(token),
                out state) && state.ExpiresUtc > DateTime.UtcNow;
        }
    }
}