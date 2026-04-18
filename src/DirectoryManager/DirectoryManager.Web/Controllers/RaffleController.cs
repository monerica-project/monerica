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
        private const string TokenExpiredMessage = "This raffle link has expired or is invalid. Please check your review confirmation.";

        private readonly IMemoryCache cache;
        private readonly IDirectoryEntryReviewRaffleEntryRepository raffleRepository;
        private readonly IDirectoryEntryReviewRepository reviewRepository;

        public RaffleController(
            IMemoryCache cache,
            IDirectoryEntryReviewRaffleEntryRepository raffleRepository,
            IDirectoryEntryReviewRepository reviewRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.cache = cache;
            this.raffleRepository = raffleRepository;
            this.reviewRepository = reviewRepository;
        }

        // ------------------------------------------------------------------
        // GET /raffle/enter?token=<guid>
        // Shows the address entry form, or redirects to AlreadyEntered.
        // ------------------------------------------------------------------
        [HttpGet("enter")]
        public async Task<IActionResult> Enter(Guid token, CancellationToken ct = default)
        {
            if (!this.TryGetRaffleToken(token, out var state))
            {
                return this.BadRequest(TokenExpiredMessage);
            }

            // Is the author already in an active raffle entry?
            var existing = await this.raffleRepository.GetActiveEntryByFingerprintAsync(state.Fingerprint, ct);
            if (existing is not null)
            {
                return this.RedirectToAction(nameof(this.AlreadyEntered));
            }

            this.ViewBag.Token = token;
            this.ViewBag.ReviewId = state.ReviewId;
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

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.Token = token;
                this.ViewBag.ReviewId = state.ReviewId;
                return this.View("Enter", input);
            }

            // Re-check: still no active entry for this author?
            var existing = await this.raffleRepository.GetActiveEntryByFingerprintAsync(state.Fingerprint, ct);
            if (existing is not null)
            {
                return this.RedirectToAction(nameof(this.AlreadyEntered));
            }

            // Verify the review still exists and belongs to this fingerprint
            var review = await this.reviewRepository.GetByIdAsync(state.ReviewId, ct);
            if (review is null || !review.AuthorFingerprint.Equals(state.Fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequest("Review not found or fingerprint mismatch.");
            }

            var entry = new DirectoryEntryReviewRaffleEntry
            {
                DirectoryEntryReviewId = state.ReviewId,
                CryptoType = input.CryptoType.Trim().ToUpperInvariant(),
                CryptoAddress = input.CryptoAddress.Trim(),
                Status = RaffleEntryStatus.Pending
            };

            await this.raffleRepository.AddAsync(entry, ct);

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
