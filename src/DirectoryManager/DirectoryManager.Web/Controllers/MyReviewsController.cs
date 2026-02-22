using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace DirectoryManager.Web.Controllers
{
    [Route("userreviews")]
    public class MyReviewsController : BaseController
    {
        private const string SessionExpiredMessage = "Session expired. Please start over.";
        private static readonly char[] CodeAlphabet = StringConstants.CodeAlphabet.ToCharArray();

        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IPgpService pgp;

        private readonly IDirectoryEntryReviewRepository reviewRepo;
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;

        public MyReviewsController(
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryReviewRepository reviewRepo,
            IDirectoryEntryReviewCommentRepository commentRepo)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.reviewRepo = reviewRepo;
            this.commentRepo = commentRepo;
        }

        // -----------------------------------------
        // Step 0: Begin (POST only)
        // -----------------------------------------
        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound();

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken]
        public IActionResult Begin([FromForm] string? website)
        {
            if (!string.IsNullOrWhiteSpace(website))
            {
                return this.BadRequest();
            }

            var flowId = this.CreateFlow();
            return this.RedirectToAction(nameof(this.Captcha), new { flowId });
        }

        // -----------------------------------------
        // Step 1: Captcha
        // -----------------------------------------
        [HttpGet("captcha")]
        public IActionResult Captcha(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.CaptchaPurpose = "myreviews";
            return this.View();
        }

        [HttpPost("captcha")]
        [ValidateAntiForgeryToken]
        public IActionResult CaptchaPost(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Captcha failed. Please try again.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.CaptchaPurpose = "myreviews";
                return this.View("Captcha");
            }

            state.CaptchaOk = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
        }

        // -----------------------------------------
        // Step 2: Submit public key
        // -----------------------------------------
        [HttpGet("submit-key")]
        public IActionResult SubmitKey(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.CaptchaOk)
            {
                return this.RedirectToAction(nameof(this.Captcha), new { flowId });
            }

            this.ViewBag.FlowId = flowId;
            return this.View();
        }

        [HttpPost("submit-key")]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitKeyPost(Guid flowId, string pgpArmored)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
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

        // -----------------------------------------
        // Step 3: Verify code
        // -----------------------------------------
        [HttpGet("verify-code")]
        public IActionResult VerifyCode(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (string.IsNullOrWhiteSpace(state.ChallengeCiphertext))
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.Ciphertext = state.ChallengeCiphertext;

            // re-use your shared partial expectations
            this.ViewBag.PostAction = nameof(this.VerifyCodePost);
            this.ViewBag.PostController = "MyReviews";

            return this.View();
        }

        [HttpGet("signin")]
        public IActionResult SignIn()
        {
            return this.View();
        }

        [HttpPost("verify-code")]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCodePost(Guid flowId, string code)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
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
                    "That code doesn’t match. Decrypt the message again and enter the code exactly as shown (dashes/spaces don’t matter).");

                this.ViewBag.FlowId = flowId;
                this.ViewBag.Ciphertext = state.ChallengeCiphertext;
                this.ViewBag.PostAction = nameof(this.VerifyCodePost);
                this.ViewBag.PostController = "MyReviews";

                this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
                return this.View("VerifyCode");
            }

            state.ChallengeSolved = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.Index), new { flowId });
        }

        // -----------------------------------------
        // Authenticated area: list reviews + replies
        // -----------------------------------------
        [HttpGet("")]
        public async Task<IActionResult> Index(Guid flowId, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.ChallengeSolved || string.IsNullOrWhiteSpace(state.PgpFingerprint))
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var fp = state.PgpFingerprint;

            var reviews = await this.reviewRepo.ListByAuthorFingerprintAsync(fp, page, pageSize, ct);
            var total = await this.reviewRepo.CountByAuthorFingerprintAsync(fp, ct);

            var vm = new MyReviewsIndexVm
            {
                Fingerprint = fp,
                Page = page < 1 ? 1 : page,
                PageSize = pageSize < 1 ? 25 : pageSize,
                TotalReviews = total
            };

            foreach (var r in reviews)
            {
                // show full thread (any status) so user sees replies they made even if pending/rejected
                var comments = await this.commentRepo.ListForReviewAnyStatusAsync(r.DirectoryEntryReviewId, ct);

                vm.Reviews.Add(new MyReviewRowVm
                {
                    Review = r,
                    Comments = comments
                });
            }

            this.ViewBag.FlowId = flowId;
            return this.View(vm);
        }

        // -----------------------------------------
        // Delete review (GET confirm)
        // -----------------------------------------
        [HttpGet("review/{id:int}/delete")]
        public async Task<IActionResult> DeleteReview(Guid flowId, int id, CancellationToken ct = default)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.ChallengeSolved || string.IsNullOrWhiteSpace(state.PgpFingerprint))
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var review = await this.reviewRepo.GetByIdAsync(id, ct);
            if (review is null || review.AuthorFingerprint != state.PgpFingerprint)
            {
                return this.NotFound();
            }

            // Ensure DirectoryEntry is loaded for display
            // (If your GetByIdAsync doesn't include it, you can do a separate lookup, or add a "GetWithEntryByIdAsync".)

            var vm = new DeleteMyReviewVm
            {
                FlowId = flowId,
                Fingerprint = state.PgpFingerprint!,
                Review = review,
                ListingName = review.DirectoryEntry?.Name ?? "Listing"
            };

            return this.View(vm);
        }

        // Delete review (POST confirm)
        [HttpPost("review/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReviewConfirmed(Guid flowId, int id, CancellationToken ct = default)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.ChallengeSolved || string.IsNullOrWhiteSpace(state.PgpFingerprint))
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var ok = await this.reviewRepo.DeleteOwnedAsync(id, state.PgpFingerprint!, ct);
            if (!ok)
            {
                return this.NotFound();
            }

            this.ClearCachedItems();
            this.TempData["SuccessMessage"] = "Review deleted.";
            return this.RedirectToAction(nameof(this.Index), new { flowId });
        }

        // -----------------------------------------
        // Delete reply (GET confirm)
        // -----------------------------------------
        [HttpGet("reply/{id:int}/delete")]
        public async Task<IActionResult> DeleteReply(Guid flowId, int id, CancellationToken ct = default)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.ChallengeSolved || string.IsNullOrWhiteSpace(state.PgpFingerprint))
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var comment = await this.commentRepo.GetByIdAsync(id, ct);
            if (comment is null || comment.AuthorFingerprint != state.PgpFingerprint)
            {
                return this.NotFound();
            }

            var parentReview = await this.reviewRepo.GetByIdAsync(comment.DirectoryEntryReviewId, ct);
            if (parentReview is null)
            {
                return this.NotFound();
            }

            var vm = new DeleteMyReplyVm
            {
                FlowId = flowId,
                Fingerprint = state.PgpFingerprint!,
                Comment = comment,
                ParentReview = parentReview,
                ListingName = parentReview.DirectoryEntry?.Name ?? "Listing"
            };

            return this.View(vm);
        }

        // Delete reply (POST confirm)
        [HttpPost("reply/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReplyConfirmed(Guid flowId, int id, CancellationToken ct = default)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            if (!state.ChallengeSolved || string.IsNullOrWhiteSpace(state.PgpFingerprint))
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var ok = await this.commentRepo.DeleteOwnedAsync(id, state.PgpFingerprint!, ct);
            if (!ok)
            {
                return this.NotFound();
            }

            this.ClearCachedItems();
            this.TempData["SuccessMessage"] = "Reply deleted.";
            return this.RedirectToAction(nameof(this.Index), new { flowId });
        }

        // -----------------------------------------
        // Helpers (copied from your controller)
        // -----------------------------------------
        private static string CacheKey(Guid flowId) => $"myreviews-flow:{flowId}";

        private Guid CreateFlow()
        {
            var id = Guid.NewGuid();
            var state = new MyReviewsFlowState
            {
                ExpiresUtc = DateTime.UtcNow.AddMinutes(IntegerConstants.SessinExpiresMinutes)
            };

            this.cache.Set(CacheKey(id), state, state.ExpiresUtc);
            return id;
        }

        private bool TryGetFlow(Guid flowId, out MyReviewsFlowState state)
        {
            return this.cache.TryGetValue(CacheKey(flowId), out state) && state.ExpiresUtc > DateTime.UtcNow;
        }

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
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
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
    }
}