using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
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

        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IPgpService pgp;
        private readonly IDirectoryEntryReviewRepository directoryEntryReviewRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IUserContentModerationService moderation;

        public DirectoryEntryReviewsController(
            IDirectoryEntryReviewRepository repo,
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryRepository directoryEntryRepository,
            IUserContentModerationService moderation)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.directoryEntryReviewRepository = repo;
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.directoryEntryRepository = directoryEntryRepository;
            this.moderation = moderation;
        }

        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound(); // don’t expose a crawlable GET

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken] // static page can’t emit a token
        public IActionResult Begin([FromForm] int directoryEntryId, [FromForm] string? website)
        {
            if (!string.IsNullOrWhiteSpace(website))
            {
                return this.BadRequest();
            }

            var flowId = this.CreateFlow(directoryEntryId);
            return this.RedirectToAction(nameof(this.Captcha), new { flowId });
        }

        [HttpGet("captcha")]
        public IActionResult Captcha(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest(SesssionExpiredMessage );
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
                return this.BadRequest(SesssionExpiredMessage );
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
                return this.BadRequest(SesssionExpiredMessage );
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
                return this.BadRequest(SesssionExpiredMessage );
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

            // ✅ stronger but still typable:
            // Store normalized (no dash) and encrypt a friendly formatted version (ABCDE-FGHIJ).
            var expectedNormalized = GenerateChallengeCodeNormalized(IntegerConstants.ChallengeLength);
            var plaintextForUser = FormatChallengeCodeForHumans(expectedNormalized);
            var cipher = this.pgp.EncryptTo(pgpArmored, plaintextForUser);

            state.PgpArmored = pgpArmored;
            state.PgpFingerprint = fp;

            // NOTE: ReviewFlowState must have:
            //   public string? ChallengeCode { get; set; }
            //   public int VerifyAttempts { get; set; }
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
                return this.BadRequest(SesssionExpiredMessage );
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
                return this.BadRequest(SesssionExpiredMessage );
            }

            if (string.IsNullOrWhiteSpace(state.ChallengeCode))
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            var submitted = NormalizeSubmittedCode(code);

            // ✅ throttle guessing per flow
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
                return this.BadRequest(SesssionExpiredMessage );
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

            return this.View(vm);
        }

        [HttpPost("compose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComposePost(Guid flowId, CreateDirectoryEntryReviewInputModel input, CancellationToken ct)
        {
            if (!this.TryGetFlow(flowId, out var flow))
            {
                return this.BadRequest(SesssionExpiredMessage );
            }

            if (!flow.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            // Always trust the flow for the entry id
            input.DirectoryEntryId = flow.DirectoryEntryId;

            if (!this.ModelState.IsValid)
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;
                return this.View("Compose", input);
            }

            // moderation rules (min detail, no html/scripts, link/blacklist => pending)
            var mod = await this.moderation.EvaluateReviewAsync(input.Body, ct);

            if (!mod.IsValid)
            {
                this.ModelState.AddModelError(nameof(input.Body), mod.ValidationErrorMessage ?? "Invalid content.");

                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;

                return this.View("Compose", input);
            }

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = flow.DirectoryEntryId,
                Rating = input.Rating!.Value,
                Body = (input.Body ?? string.Empty).Trim(),
                CreateDate = DateTime.UtcNow,
                AuthorFingerprint = flow.PgpFingerprint,
                CreatedByUserId = "automated"
            };

            // ✅ single textbox parsing: OrderProof -> OrderId/OrderUrl
            ApplyOrderProof(entity, input.OrderProof);

            // ✅ force pending if proof is supplied, regardless of moderation outcome
            var hasOrderProof =
                !string.IsNullOrWhiteSpace(entity.OrderId) ||
                !string.IsNullOrWhiteSpace(entity.OrderUrl);

            entity.ModerationStatus = (mod.NeedsManualReview || hasOrderProof)
                ? ReviewModerationStatus.Pending
                : ReviewModerationStatus.Approved;

            await this.directoryEntryReviewRepository.AddAsync(entity, ct);

            // ✅ Your Thanks.cshtml reads TempData["ReviewMessage"]
            this.TempData["ReviewMessage"] = mod.ThankYouMessage;

            this.ClearCachedItems();
            this.cache.Remove(CacheKey(flowId));

            return this.RedirectToAction(nameof(this.Thanks));
        }

        [HttpGet("thanks")]
        public IActionResult Thanks() => this.View();

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

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = input.DirectoryEntryId,
                Rating = input.Rating,
                Body = (input.Body ?? string.Empty).Trim(),
                CreateDate = DateTime.UtcNow,
                ModerationStatus = ReviewModerationStatus.Pending
            };

            // ✅ single textbox parsing: OrderProof -> OrderId/OrderUrl
            ApplyOrderProof(entity, input.OrderProof);

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

        [HttpGet("{id:int}/edit")]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id, ct);
            if (item is null)
            {
                return this.NotFound();
            }

            // NOTE: this admin Edit action is still using the entity directly.
            // If you switch admin edit to a VM (recommended for tags), map OrderProof similarly.
            item.OrderId = item.OrderId; // no-op, just clarifying
            item.OrderUrl = item.OrderUrl;

            return this.View(item);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DirectoryEntryReview model, [FromForm] string? orderProof, CancellationToken ct = default)
        {
            var pk = model.DirectoryEntryReviewId;
            if (id != pk)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            // ✅ If you keep posting entity, we still parse the single textbox separately
            ApplyOrderProof(model, orderProof);

            // Optional: if admin adds proof and review is approved, force back to pending
            var hasProof = !string.IsNullOrWhiteSpace(model.OrderId) || !string.IsNullOrWhiteSpace(model.OrderUrl);
            if (hasProof && model.ModerationStatus == ReviewModerationStatus.Approved)
            {
                model.ModerationStatus = ReviewModerationStatus.Pending;
            }

            await this.directoryEntryReviewRepository.UpdateAsync(model, ct);
            return this.RedirectToAction(nameof(this.Index));
        }

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
        // ✅ Challenge helpers
        // =========================

        private static string GenerateChallengeCodeNormalized(int length)
        {
            if (length < 6)
            {
                length = 6;
            }

            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            return new string(chars);
        }

        private static string FormatChallengeCodeForHumans(string normalized)
        {
            // 10 chars -> 5-5 (ABCDE-FGHIJ)
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 5)
            {
                return normalized;
            }

            return normalized.Substring(0, 5) + "-" + normalized.Substring(5);
        }

        private static string NormalizeSubmittedCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

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
            if (string.IsNullOrEmpty(submitted) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (submitted.Length != expected.Length)
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(submitted);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static string CacheKey(Guid flowId) => $"review-flow:{flowId}";

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