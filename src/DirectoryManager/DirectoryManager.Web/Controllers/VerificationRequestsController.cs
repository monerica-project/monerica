using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.VerificationRequests;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    // Public, captcha-gated flow for requesting that Monerica verify a listing.
    [Route("verification-requests")]
    public class VerificationRequestsController : BaseController
    {
        private const string CacheKeyPrefix = "verifreq-flow:";

        private readonly IMemoryCache cache;
        private readonly ICaptchaService captcha;
        private readonly IDirectoryEntryRepository entries;
        private readonly IVerificationRequestRepository requests;

        public VerificationRequestsController(
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IDirectoryEntryRepository entries,
            IVerificationRequestRepository requests)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.cache = cache;
            this.captcha = captcha;
            this.entries = entries;
            this.requests = requests;
        }

        private static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id}";

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

            var entry = await this.entries.GetByIdAsync(directoryEntryId);
            if (entry is null)
            {
                return this.NotFound();
            }

            var id = Guid.NewGuid();
            var state = new VerificationRequestFlowState
            {
                DirectoryEntryId = directoryEntryId,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(IntegerConstants.SessinExpiresMinutes)
            };
            this.cache.Set(CacheKey(id), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.Captcha), new { flowId = id });
        }

        [HttpGet("captcha")]
        public IActionResult Captcha(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out _))
            {
                return this.Redirect("/");
            }

            this.ViewBag.FlowId = flowId;
            return this.View();
        }

        [HttpPost("captcha")]
        [ValidateAntiForgeryToken]
        public IActionResult CaptchaPost(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state))
            {
                return this.Redirect("/");
            }

            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Captcha was incorrect, please try again.");
                this.ViewBag.FlowId = flowId;
                return this.View("Captcha");
            }

            state.CaptchaOk = true;
            this.cache.Set(CacheKey(flowId), state, state.ExpiresUtc);

            return this.RedirectToAction(nameof(this.Compose), new { flowId });
        }

        [HttpGet("compose")]
        public async Task<IActionResult> Compose(Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var state) || !state.CaptchaOk)
            {
                return this.RedirectToAction(nameof(this.Captcha), new { flowId });
            }

            var entry = await this.entries.GetByIdAsync(state.DirectoryEntryId);
            if (entry is null)
            {
                return this.NotFound();
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.EntryName = entry.Name;
            return this.View(new CreateVerificationRequestInputModel());
        }

        [HttpPost("compose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComposePost(Guid flowId, CreateVerificationRequestInputModel input, CancellationToken ct)
        {
            if (!this.TryGetFlow(flowId, out var state) || !state.CaptchaOk)
            {
                return this.RedirectToAction(nameof(this.Captcha), new { flowId });
            }

            if (!string.IsNullOrWhiteSpace(input.Website))
            {
                return this.BadRequest();
            }

            var entry = await this.entries.GetByIdAsync(state.DirectoryEntryId);
            if (entry is null)
            {
                return this.NotFound();
            }

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.FlowId = flowId;
                this.ViewBag.EntryName = entry.Name;
                return this.View("Compose", input);
            }

            await this.requests.AddAsync(
                new VerificationRequest
                {
                    DirectoryEntryId = state.DirectoryEntryId,
                    Comment = input.Comment.Trim(),
                    Status = VerificationRequestStatus.Pending
                },
                ct);

            this.cache.Remove(CacheKey(flowId));
            return this.RedirectToAction(nameof(this.Thanks));
        }

        [HttpGet("thanks")]
        public IActionResult Thanks() => this.View();

        private bool TryGetFlow(Guid flowId, out VerificationRequestFlowState state)
        {
            return this.cache.TryGetValue(CacheKey(flowId), out state!) && state.ExpiresUtc > DateTime.UtcNow;
        }
    }
}
