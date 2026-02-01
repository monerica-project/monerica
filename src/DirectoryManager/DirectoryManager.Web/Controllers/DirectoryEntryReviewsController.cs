using System.Security.Cryptography;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Route("directory-entry-reviews")]
    public class DirectoryEntryReviewsController : Controller
    {
        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IPgpService pgp;
        private readonly IDirectoryEntryReviewRepository directoryEntryReviewRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;

        public DirectoryEntryReviewsController(
            IDirectoryEntryReviewRepository repo,
            IMemoryCache cache,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            this.directoryEntryReviewRepository = repo;
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.directoryEntryRepository = directoryEntryRepository;
        }

        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound(); // don’t expose a crawlable GET

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken] // static page can’t emit a token
        public IActionResult Begin([FromForm] int directoryEntryId, [FromForm] string? website)
        {
            // simple honeypot check (optional)
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
                return this.BadRequest("Session expired.");
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;

            // ✅ IMPORTANT: tells the view what this flow is
            this.ViewBag.CaptchaPurpose = "review";

            return this.View();
        }

        [HttpPost("captcha")]
        [ValidateAntiForgeryToken]
        public IActionResult CaptchaPost(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest("Session expired.");
            }

            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Captcha failed. Please try again.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;

                // ✅ IMPORTANT: keep purpose on re-render
                this.ViewBag.CaptchaPurpose = "review";

                return this.View("Captcha");
            }

            state.CaptchaOk = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
        }

        // Step 2: submit PGP key (GET/POST)
        [HttpGet("submit-key")]
        public IActionResult SubmitKey(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest("Session expired.");
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
                return this.BadRequest("Session expired.");
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

            // Generate 6-digit code and encrypt it to their key
            int code = SixDigits();
            string cipher = this.pgp.EncryptTo(pgpArmored, code.ToString());

            state.PgpArmored = pgpArmored;
            state.PgpFingerprint = fp;
            state.ChallengeCode = code;
            state.ChallengeCiphertext = cipher;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
        }

        // Step 3: show ciphertext and collect decrypted code
        [HttpGet("verify-code")]
        public IActionResult VerifyCode(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest("Session expired.");
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
                return this.BadRequest("Session expired.");
            }

            if (state.ChallengeCode is null)
            {
                return this.RedirectToAction(nameof(this.SubmitKey), new { flowId });
            }

            if (!int.TryParse(code, out var numeric) || numeric != state.ChallengeCode.Value)
            {
                this.ModelState.AddModelError(string.Empty, "That code doesn’t match. Decrypt the message with your private key and try again.");
                this.ViewBag.FlowId = flowId;
                this.ViewBag.Ciphertext = state.ChallengeCiphertext;
                this.ViewBag.DirectoryEntryId = state.DirectoryEntryId;
                return this.View("VerifyCode");
            }

            state.ChallengeSolved = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);
            return this.RedirectToAction(nameof(this.Compose), new { flowId });
        }

        // Step 4: compose review (GET/POST)
        [HttpGet("compose")]
        public async Task<IActionResult> Compose(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.BadRequest("Session expired.");
            }

            if (!state.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            var vm = new CreateDirectoryEntryReviewInputModel
            {
                DirectoryEntryId = state.DirectoryEntryId,
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
                return this.BadRequest("Session expired.");
            }

            if (!flow.ChallengeSolved)
            {
                return this.RedirectToAction(nameof(this.VerifyCode), new { flowId });
            }

            if (!this.ModelState.IsValid)
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                this.ViewBag.DirectoryEntryName = entry?.Name ?? "Listing";
                this.ViewBag.FlowId = flowId;
                this.ViewBag.PgpFingerprint = flow.PgpFingerprint;

                input.DirectoryEntryId = flow.DirectoryEntryId;
                return this.View("Compose", input);
            }

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = flow.DirectoryEntryId,
                Rating = input.Rating!.Value,
                Body = input.Body.Trim(),
                CreateDate = DateTime.UtcNow,
                ModerationStatus = DirectoryManager.Data.Enums.ReviewModerationStatus.Pending,
                AuthorFingerprint = flow.PgpFingerprint
            };

            await this.directoryEntryReviewRepository.AddAsync(entity, ct);
            this.cache.Remove(CacheKey(flowId));
            return this.RedirectToAction(nameof(this.Thanks));
        }

        // Step 5: thank you
        [HttpGet("thanks")]
        public IActionResult Thanks() => this.View();

        [HttpGet("")] // GET /directory-entry-reviews
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var items = await this.directoryEntryReviewRepository.ListAsync(page, pageSize);
            this.ViewBag.Total = await this.directoryEntryReviewRepository.CountAsync();
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(items);
        }

        [HttpGet("{id:int}")] // GET /directory-entry-reviews/123
        public async Task<IActionResult> Details(int id)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpGet("create")]
        public IActionResult Create() => this.View(new DirectoryEntryReview());

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDirectoryEntryReviewInputModel input, CancellationToken ct)
        {
            if (!this.ModelState.IsValid)
            {
                // ModelState contains field errors; the view will render them.
                return this.View(input);
            }

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = input.DirectoryEntryId,
                Rating = input.Rating,
                Body = input.Body.Trim(),
                CreateDate = DateTime.UtcNow
            };

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

        [HttpGet("{id:int}/edit")] // GET /directory-entry-reviews/123/edit
        public async Task<IActionResult> Edit(int id)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DirectoryEntryReview model)
        {
            // replace DirectoryEntryReviewId with your PK if named differently
            var pk = model.DirectoryEntryReviewId;
            if (id != pk)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            await this.directoryEntryReviewRepository.UpdateAsync(model);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("{id:int}/delete")] // POST /directory-entry-reviews/123/delete
        public async Task<IActionResult> Delete(int id)
        {
            var item = await this.directoryEntryReviewRepository.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await this.directoryEntryReviewRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        private static int SixDigits()
        {
            return RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        }

        private static string CacheKey(Guid flowId) => $"review-flow:{flowId}";

        private Guid CreateFlow(int directoryEntryId)
        {
            var id = Guid.NewGuid();
            var state = new ReviewFlowState
            {
                DirectoryEntryId = directoryEntryId,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(20)
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