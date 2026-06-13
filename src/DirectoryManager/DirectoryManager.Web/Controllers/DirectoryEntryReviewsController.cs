using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Route("directory-entry-reviews")]
    public class DirectoryEntryReviewsController : BaseController
    {
        private const string SesssionExpiredMessage = "Session expired, your reply message was not saved, please start over!";
        private static readonly char[] CodeAlphabet = StringConstants.CodeAlphabet.ToCharArray();

        // Prevents double-POST without cookies or JS.
        private static readonly ConcurrentDictionary<Guid, bool> SubmittedFlows = new ();

        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IPgpService pgp;
        private readonly IDirectoryEntryReviewRepository directoryEntryReviewRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IUserContentModerationService moderation;
        private readonly IRaffleRepository raffleRepository;
        private readonly ISubcategoryRepository subcategoryRepository;

        public DirectoryEntryReviewsController(
            IDirectoryEntryReviewRepository repo,
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryRepository directoryEntryRepository,
            IUserContentModerationService moderation,
            IRaffleRepository raffleRepository,
            ISubcategoryRepository subcategoryRepository)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.directoryEntryReviewRepository = repo;
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.directoryEntryRepository = directoryEntryRepository;
            this.moderation = moderation;
            this.raffleRepository = raffleRepository;
            this.subcategoryRepository = subcategoryRepository;
        }

        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound();

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Begin([FromForm] int directoryEntryId, [FromForm] string? website)
        {
            if (!string.IsNullOrWhiteSpace(website))
            {
                return this.BadRequest();
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (entry is null || entry.ReviewsDisabled)
            {
                return this.NotFound();
            }

            var flowId = this.CreateFlow(directoryEntryId);
            return this.RedirectToAction(nameof(this.Captcha), new { flowId });
        }

        [HttpGet("captcha")]
        public IActionResult Captcha(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;
            this.ViewBag.CaptchaPurpose = "review";

            return this.View();
        }

        [HttpPost("captcha")]
        [ValidateAntiForgeryToken]
        public IActionResult CaptchaPost(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Captcha failed. Please try again.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;
                this.ViewBag.CaptchaPurpose = "review";
                return this.View("Captcha");
            }

            state.CaptchaOk = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
        }

        [HttpGet("submit-key")]
        public IActionResult SubmitKey(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!state.CaptchaOk)
            {
                return this.RedirectToAction(nameof(this.Captcha), new { flowId });
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;
            return this.View();
        }

        [HttpPost("submit-key")]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitKeyPost(Guid flowId, string pgpArmored)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!state.CaptchaOk)
            {
                return this.RedirectToAction(nameof(this.Captcha), new { flowId });
            }

            var fp = this.pgp.GetFingerprint(pgpArmored);
            if (string.IsNullOrWhiteSpace(fp))
            {
                this.ModelState.AddModelError(string.Empty, "Invalid PGP public key.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;
                return this.View("SubmitKey");
            }

            var expectedNormalized = GenerateChallengeCodeNormalized(IntegerConstants.ChallengeLength);
            var plaintextForUser = FormatChallengeCodeForHumans(expectedNormalized);
            var cipher = this.pgp.EncryptTo(pgpArmored, plaintextForUser);

            state.PgpArmored = pgpArmored;
            state.PgpFingerprint = fp;
            state.ChallengeCode = expectedNormalized;
            state.ChallengeCiphertext = cipher;
            state.VerifyAttempts = 0;

            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
        }

        [HttpGet("verify-code")]
        public IActionResult VerifyCode(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (string.IsNullOrWhiteSpace(state.ChallengeCiphertext))
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.Ciphertext = state.ChallengeCiphertext;
            this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;

            return this.View();
        }

        [HttpPost("verify-code")]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCodePost(Guid flowId, string code)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (string.IsNullOrWhiteSpace(state.ChallengeCode))
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            var submitted = NormalizeSubmittedCode(code);

            state.VerifyAttempts++;
            if (state.VerifyAttempts > IntegerConstants.MaxVerifyAttempts)
            {
                this.cache.Remove(CacheKey(flowId));
                return this.BadRequest("Too many attempts. Please start over.");
            }

            if (!CodesMatchConstantTime(submitted, state.ChallengeCode))
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    "That code doesn't match. Decrypt the message again and enter the code exactly as shown (dashes/spaces don't matter).");

                this.ViewBag.FlowId = flowId;
                this.ViewBag.Ciphertext = state.ChallengeCiphertext;
                this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;

                this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
                return this.View("VerifyCode");
            }

            state.ChallengeSolved = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.Compose), new { flowId });
        }

        [HttpGet("compose")]
        public async Task<IActionResult> Compose(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!state.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var vm = new CreateDirectoryEntryReviewInputModel
            {
                DirectoryEntryId = state.DirectoryEntryId
            };

            var entry = await this.directoryEntryRepository.GetByIdAsync(state.DirectoryEntryId);
            this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
            this.ViewBag.FlowId = flowId;
            this.ViewBag.PgpFingerprint = state.PgpFingerprint;
            this.ViewBag.RequireVerification =
                entry is not null && await this.SubcategoryRequiresVerificationAsync(entry.SubCategoryId);

            return this.View(vm);
        }

        [HttpPost("compose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComposePost(Guid flowId, CreateDirectoryEntryReviewInputModel input, CancellationToken ct)
        {
            if (!SubmittedFlows.TryAdd(flowId, true))
            {
                return this.RedirectToAction(nameof(this.Thanks));
            }

            if (!this.TryGetFlow(flowId, out var flow))
            {
                SubmittedFlows.TryRemove(flowId, out _);
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!flow.ChallengeSolved)
            {
                SubmittedFlows.TryRemove(flowId, out _);
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            input.DirectoryEntryId = flow.DirectoryEntryId;

            var gateEntry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
            if (gateEntry is null || gateEntry.ReviewsDisabled)
            {
                SubmittedFlows.TryRemove(flowId, out _);
                this.cache.Remove(CacheKey(flowId));
                return this.NotFound();
            }

            // Does this listing's subcategory force order-proof verification + manual moderation?
            // Re-checked here (not just in the GET) so the requirement can't be bypassed.
            var requireVerification = await this.SubcategoryRequiresVerificationAsync(gateEntry.SubCategoryId);
            this.ViewBag.RequireVerification = requireVerification;

            if (!this.ModelState.IsValid)
            {
                SubmittedFlows.TryRemove(flowId, out _);
                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;
                return this.View("Compose", input);
            }

            // 🚫 Duplicate body guard — blocks the same review text from being posted twice,
            // even by the same author (same PGP fingerprint).
            var normalizedBody = UnicodeSanitizer.CleanMultiLine(input.Body);
            if (await this.directoryEntryReviewRepository.ExistsByBodyAsync(normalizedBody, null, ct))
            {
                SubmittedFlows.TryRemove(flowId, out _);
                this.ModelState.AddModelError(
                    nameof(input.Body),
                    "A review with this exact message has already been submitted. Please write something original.");

                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;
                return this.View("Compose", input);
            }

            // When the subcategory requires verification, the order-proof URL is mandatory.
            if (requireVerification && string.IsNullOrWhiteSpace(input.OrderProof))
            {
                SubmittedFlows.TryRemove(flowId, out _);
                this.ModelState.AddModelError(
                    nameof(input.OrderProof),
                    "An order proof URL is required for reviews in this category (for example https://shop.example.com/orders/123).");

                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;
                return this.View("Compose", input);
            }

            // Order proof is optional, but if supplied it must be a URL.
            if (!TryNormalizeOrderProofUrl(input.OrderProof, out _))
            {
                SubmittedFlows.TryRemove(flowId, out _);
                this.ModelState.AddModelError(
                    nameof(input.OrderProof),
                    "Order proof must be a URL (for example https://shop.example.com/orders/123). Leave it blank if you don't have one.");

                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;
                return this.View("Compose", input);
            }

            var mod = await this.moderation.EvaluateReviewAsync(input.Body, ct);

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = flow.DirectoryEntryId,
                Rating = input.Rating!.Value,
                Body = (input.Body ?? string.Empty).Trim(),
                CreateDate = DateTime.UtcNow,
                AuthorFingerprint = flow.PgpFingerprint,
                CreatedByUserId = "automated"
            };

            if (!mod.IsValid)
            {
                entity.ModerationStatus = ReviewModerationStatus.Rejected;
                await this.directoryEntryReviewRepository.AddAsync(entity, ct);

                this.TempData["ReviewMessage"] = mod.ThankYouMessage;
                this.cache.Remove(CacheKey(flowId));

                // Only send them to the raffle entry page if there's actually a live raffle.
                return await this.RedirectToRaffleOrThanksAsync(
                    entity.DirectoryEntryReviewId,
                    flow.PgpFingerprint,
                    ct);
            }

            // Capture any order link the reviewer supplied (already validated as a URL above)
            // so it can be inspected during moderation. It is never shown publicly.
            ApplyOrderProof(entity, input.OrderProof);
            entity.OrderProofContext = NormalizeOrderProofContext(input.OrderProofContext);

            // Verification subcategories always go to manual moderation. Otherwise, auto-publish
            // clean reviews: a review goes live immediately when it has neither a blacklist term
            // nor a hyperlink in the body (mod.NeedsManualReview is exactly hasBlacklistTerm ||
            // hasLink). Anything that trips either trigger is held for manual moderation.
            if (requireVerification || mod.NeedsManualReview)
            {
                entity.ModerationStatus = ReviewModerationStatus.Pending;
            }
            else
            {
                entity.ModerationStatus = ReviewModerationStatus.Approved;
                entity.UpdatedByUserId = "automated";
            }

            await this.directoryEntryReviewRepository.AddAsync(entity, ct);

            this.TempData["ReviewMessage"] = mod.ThankYouMessage;

            this.ClearCachedItems();
            this.cache.Remove(CacheKey(flowId));

            return await this.RedirectToRaffleOrThanksAsync(
                entity.DirectoryEntryReviewId,
                flow.PgpFingerprint,
                ct);
        }

        [HttpGet("thanks")]
        public IActionResult Thanks() => this.View();

        // ---------------------------
        // Admin CRUD
        // ---------------------------

        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var items = await this.directoryEntryReviewRepository.ListAsync(page, pageSize, ct);
            this.ViewBag.Total = await this.directoryEntryReviewRepository.CountAsync(ct);
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(items);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpGet("create")]
        public IActionResult Create() => this.View(new CreateDirectoryEntryReviewInputModel());

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDirectoryEntryReviewInputModel input, CancellationToken ct)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(input);
            }

            var normalizedBody = (input.Body ?? string.Empty).Trim();
            if (await this.directoryEntryReviewRepository.ExistsByBodyAsync(normalizedBody, null, ct))
            {
                this.ModelState.AddModelError(
                    nameof(input.Body),
                    "A review with this exact message already exists.");
                return this.View(input);
            }

            // Order proof is optional, but if supplied it must be a URL.
            if (!TryNormalizeOrderProofUrl(input.OrderProof, out _))
            {
                this.ModelState.AddModelError(
                    nameof(input.OrderProof),
                    "Order proof must be a URL (for example https://shop.example.com/orders/123). Leave it blank if there isn't one.");
                return this.View(input);
            }

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = input.DirectoryEntryId,
                Rating = input.Rating,
                Body = (input.Body ?? string.Empty).Trim(),
                CreateDate = DateTime.UtcNow,
                ModerationStatus = ReviewModerationStatus.Pending
            };

            ApplyOrderProof(entity, input.OrderProof);
            entity.OrderProofContext = NormalizeOrderProofContext(input.OrderProofContext);

            try
            {
                await this.directoryEntryReviewRepository.AddAsync(entity, ct);
                this.TempData["SuccessMessage"] = "Review created.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save the review. Please check your inputs and try again.");
                return this.View(input);
            }
            catch
            {
                this.ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return this.View(input);
            }
        }

        // Old edit path — now lives in ReviewModerationController.
        // Keep a redirect so any existing Edit buttons / bookmarks still work.
        [HttpGet("{id:int}/edit")]
        public IActionResult Edit(int id)
            => this.RedirectToAction("Edit", "ReviewModeration", new { id });

        [HttpPost("{id:int}/delete")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpPost("{id:int}/delete/confirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            await this.directoryEntryReviewRepository.DeleteAsync(id, ct);
            return this.RedirectToAction(nameof(this.Index));
        }

        // =========================
        // Raffle token helpers
        // =========================

        internal static string RaffleTokenCacheKey(Guid token) => $"raffle-token:{token}";

        private Guid CreateRaffleToken(int reviewId, string fingerprint)
        {
            var token = Guid.NewGuid();
            var state = new RaffleFlowState
            {
                ReviewId = reviewId,
                Fingerprint = fingerprint,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(IntegerConstants.SessinExpiresMinutes)
            };

            this.cache.Set(RaffleTokenCacheKey(token), state, state.ExpiresUtc);
            return token;
        }

        /// <summary>
        /// If a raffle is currently active (enabled + within its [StartDate, EndDate] window),
        /// mint a token and send the user to the raffle address-entry page. Otherwise send them
        /// to the Thanks page so they never see a way to submit a payout address for a closed raffle.
        /// </summary>
        private async Task<IActionResult> RedirectToRaffleOrThanksAsync(
            int reviewId,
            string fingerprint,
            CancellationToken ct)
        {
            var active = await this.raffleRepository.GetActiveAsync(DateTime.UtcNow, ct);
            if (active is null)
            {
                return this.RedirectToAction(nameof(this.Thanks));
            }

            var token = this.CreateRaffleToken(reviewId, fingerprint);
            return this.RedirectToAction("Enter", "Raffle", new { token });
        }

        // =========================
        // Challenge code helpers
        // =========================

        private static string GenerateChallengeCodeNormalized(int length)
        {
            if (length < 6) length = 6;
            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }
            return new string(chars);
        }

        private static string FormatChallengeCodeForHumans(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 5) return normalized;
            return normalized.Substring(0, 5) + "-" + normalized.Substring(5);
        }

        private static string NormalizeSubmittedCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            }
            return sb.ToString();
        }

        private static bool CodesMatchConstantTime(string submitted, string expected)
        {
            if (string.IsNullOrEmpty(submitted) || string.IsNullOrEmpty(expected)) return false;
            if (submitted.Length != expected.Length) return false;
            var a = Encoding.UTF8.GetBytes(submitted);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        // =========================
        // Flow helpers
        // =========================

        private static string CacheKey(Guid flowId) => $"review-flow:{flowId}";

        /// <summary>
        /// Order proof is optional, but when supplied it MUST be a URL. An absolute
        /// http/https URL is taken as-is; a bare domain/path (e.g. "shop.example.com/orders/123")
        /// is accepted and promoted to https://. Anything else (free text, an order id, etc.)
        /// is rejected so the caller can surface a validation error.
        /// Returns true when the value is absent (valid: nothing to store) or a valid URL,
        /// and false when a non-empty value is not a URL.
        /// </summary>
        private static bool TryNormalizeOrderProofUrl(string? orderProof, out string? normalizedUrl)
        {
            normalizedUrl = null;
            var s = (orderProof ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(s))
            {
                return true;
            }

            if (Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                normalizedUrl = s;
                return true;
            }

            if (!s.Contains(' ') &&
                s.Contains('.') &&
                Uri.TryCreate("https://" + s.TrimStart('/'), UriKind.Absolute, out var uri2) &&
                (uri2.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri2.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                normalizedUrl = uri2.ToString();
                return true;
            }

            return false;
        }

        private static void ApplyOrderProof(DirectoryEntryReview review, string? orderProof)
        {
            // Order proof is validated to be a URL before this is called, so we only
            // ever store a URL now (OrderId is no longer populated from user input).
            TryNormalizeOrderProofUrl(orderProof, out var url);
            review.OrderUrl = url;
            review.OrderId = null;
        }

        // Trims the optional verification-context free-text; null when empty so the column
        // stays NULL rather than holding an empty string.
        private static string? NormalizeOrderProofContext(string? context)
        {
            var s = (context ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // True when the listing's subcategory is flagged to require order-proof verification.
        private async Task<bool> SubcategoryRequiresVerificationAsync(int subCategoryId)
        {
            var sub = await this.subcategoryRepository.GetByIdAsync(subCategoryId);
            return sub?.RequireReviewVerification ?? false;
        }

        private Guid CreateFlow(int directoryEntryId)
        {
            var id = Guid.NewGuid();
            var state = new ReviewFlowState
            {
                DirectoryEntryId = directoryEntryId,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(IntegerConstants.SessinExpiresMinutes)
            };
            this.cache.Set(CacheKey(id), state, state.ExpiresUtc);
            return id;
        }

        private bool TryGetFlow(Guid flowId, out ReviewFlowState state)
        {
            return this.cache.TryGetValue(CacheKey(flowId), out state) && state.ExpiresUtc > DateTime.UtcNow;
        }
    }
}