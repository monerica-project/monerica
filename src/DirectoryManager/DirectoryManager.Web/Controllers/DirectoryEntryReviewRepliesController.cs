using System.Security.Cryptography;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Route("directory-entry-review-replies")]
    public class DirectoryEntryReviewRepliesController : BaseController
    {
        private const string SesssionExpiredMessage = "Session expired, your review message was not saved, please start over!";
        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IPgpService pgp;
        private readonly IDirectoryEntryReviewRepository reviewRepo;
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly IUserContentModerationService moderation;

        public DirectoryEntryReviewRepliesController(
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryReviewRepository reviewRepo,
            IDirectoryEntryReviewCommentRepository commentRepo,
            IDirectoryEntryRepository entryRepo,
            IUserContentModerationService moderation)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.reviewRepo = reviewRepo;
            this.commentRepo = commentRepo;
            this.entryRepo = entryRepo;
            this.moderation = moderation;
        }

        // POST-only begin (from review list)
        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound();

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Begin([FromForm] int directoryEntryReviewId, [FromForm] string? website, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(website))
            {
                return this.BadRequest();
            }

            var review = await this.reviewRepo.GetByIdAsync(directoryEntryReviewId, ct);
            if (review is null)
            {
                return this.NotFound();
            }

            var flowId = this.CreateFlow(review.DirectoryEntryReviewId, review.DirectoryEntryId);
            return this.RedirectToAction(nameof(this.Captcha), new { flowId });
        }

        // Step 1: captcha
        [HttpGet("captcha")]
        public IActionResult Captcha(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;

            // tell the shared view where to POST
            this.ViewBag.PostController = "DirectoryEntryReviewReplies";
            this.ViewBag.PostAction = "CaptchaPost";

            // IMPORTANT: this is a REPLY flow
            this.ViewBag.CaptchaPurpose = "reply";

            return this.View("~/Views/DirectoryEntryReviews/Captcha.cshtml");
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
                this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;

                this.ViewBag.PostController = "DirectoryEntryReviewReplies";
                this.ViewBag.PostAction = "CaptchaPost";
                this.ViewBag.CaptchaPurpose = "reply";

                return this.View("~/Views/DirectoryEntryReviews/Captcha.cshtml");
            }

            state.CaptchaOk = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
        }

        // Step 2: PGP key
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
            this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;
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
                this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;
                return this.View("SubmitKey");
            }

            int code = SixDigits();
            string cipher = this.pgp.EncryptTo(pgpArmored, code.ToString());

            state.PgpArmored = pgpArmored;
            state.PgpFingerprint = fp;
            state.ChallengeCode = code;
            state.ChallengeCiphertext = cipher;

            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
        }

        // Step 3: verify code
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
            this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;
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

            if (state.ChallengeCode is null)
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            if (!int.TryParse(code, out var numeric) || numeric != state.ChallengeCode.Value)
            {
                this.ModelState.AddModelError(string.Empty, "That code doesn’t match. Decrypt the message and try again.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.Ciphertext = state.ChallengeCiphertext;
                this.ViewBag.DirectoryEntryReviewId = state.DirectoryEntryReviewId;
                return this.View("VerifyCode");
            }

            state.ChallengeSolved = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.Compose), new { flowId });
        }

        // Step 4: compose reply
        [HttpGet("compose")]
        public async Task<IActionResult> Compose(Guid flowId, CancellationToken ct)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!state.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var entry = await this.entryRepo.GetByIdAsync(state.DirectoryEntryId);

            this.ViewBag.FlowId = flowId;
            this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
            this.ViewBag.PgpFingerprint = state.PgpFingerprint;

            var vm = new CreateDirectoryEntryReviewReplyInputModel
            {
                DirectoryEntryReviewId = state.DirectoryEntryReviewId
            };

            return this.View(vm);
        }

        [HttpPost("compose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComposePost(Guid flowId, CreateDirectoryEntryReviewReplyInputModel input, CancellationToken ct)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage);
            }

            if (!state.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            // Always trust the flow for the review id
            input.DirectoryEntryReviewId = state.DirectoryEntryReviewId;

            // Run moderation rules via shared service
            var mod = await this.moderation.EvaluateReplyAsync(input.Body, ct);

            if (!mod.IsValid)
            {
                this.ModelState.AddModelError(nameof(input.Body), mod.ValidationErrorMessage ?? "Invalid reply.");

                var entry = await this.entryRepo.GetByIdAsync(state.DirectoryEntryId);
                this.ViewBag.FlowId = flowId;
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.PgpFingerprint = state.PgpFingerprint;

                return this.View("Compose", input);
            }

            var bodyTrimmed = (input.Body ?? string.Empty).Trim();

            var entity = new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = state.DirectoryEntryReviewId,
                ParentCommentId = input.ParentCommentId,
                Body = bodyTrimmed,

                ModerationStatus = mod.NeedsManualReview
                    ? ReviewModerationStatus.Pending
                    : ReviewModerationStatus.Approved,

                AuthorFingerprint = state.PgpFingerprint!,
                CreateDate = DateTime.UtcNow,
                CreatedByUserId = "automated"
            };

            await this.commentRepo.AddAsync(entity, ct);

            this.ClearCachedItems();
            this.cache.Remove(CacheKey(flowId));

            // ✅ Your Replies Thanks view reads TempData["ReplyMessage"]
            this.TempData["ReplyMessage"] = mod.ThankYouMessage;

            return this.RedirectToAction(nameof(this.Thanks));
        }

        [HttpGet("thanks")]
        public IActionResult Thanks() => this.View();

        private static int SixDigits() => RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        private static string CacheKey(Guid flowId) => $"review-reply-flow:{flowId}";

        private Guid CreateFlow(int reviewId, int entryId)
        {
            var id = Guid.NewGuid();
            var state = new ReviewReplyFlowState
            {
                DirectoryEntryReviewId = reviewId,
                DirectoryEntryId = entryId,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(20)
            };
            this.cache.Set(CacheKey(id), state, state.ExpiresUtc);
            return id;
        }

        private bool TryGetFlow(Guid flowId, out ReviewReplyFlowState state) =>
            this.cache.TryGetValue(CacheKey(flowId), out state) && state.ExpiresUtc > DateTime.UtcNow;
    }
}
