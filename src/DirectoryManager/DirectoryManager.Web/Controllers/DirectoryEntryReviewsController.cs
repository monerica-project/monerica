using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
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
        private const string VerifiedOrderTagSlug = "verified-order";
        private const string OrderProofHttpClientName = "OrderProofVerifier";
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
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IReviewTagRepository reviewTagRepository;
        private readonly IRaffleRepository raffleRepository;

        public DirectoryEntryReviewsController(
            IDirectoryEntryReviewRepository repo,
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IPgpService pgp,
            IDirectoryEntryRepository directoryEntryRepository,
            IUserContentModerationService moderation,
            IHttpClientFactory httpClientFactory,
            IReviewTagRepository reviewTagRepository,
            IRaffleRepository raffleRepository)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.directoryEntryReviewRepository = repo;
            this.cache = cache;
            this.captcha = captcha;
            this.pgp = pgp;
            this.directoryEntryRepository = directoryEntryRepository;
            this.moderation = moderation;
            this.httpClientFactory = httpClientFactory;
            this.reviewTagRepository = reviewTagRepository;
            this.raffleRepository = raffleRepository;
        }

        [HttpGet("begin")]
        public IActionResult BeginGet() => this.NotFound();

        [HttpPost("begin")]
        [IgnoreAntiforgeryToken]
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
            var normalizedBody = (input.Body ?? string.Empty).Trim();
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

            ApplyOrderProof(entity, input.OrderProof);

            var hasOrderProof =
                !string.IsNullOrWhiteSpace(entity.OrderId) ||
                !string.IsNullOrWhiteSpace(entity.OrderUrl);

            var proofAutoApproved = false;

            if (!mod.NeedsManualReview && !string.IsNullOrWhiteSpace(entity.OrderUrl))
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(flow.DirectoryEntryId);
                if (entry != null)
                {
                    proofAutoApproved = await this.TryVerifyOrderUrlForEntryAsync(entry, entity.OrderUrl!, ct);
                }
            }

            entity.ModerationStatus = mod.NeedsManualReview
                ? ReviewModerationStatus.Pending
                : (hasOrderProof
                    ? (proofAutoApproved ? ReviewModerationStatus.Approved : ReviewModerationStatus.Pending)
                    : ReviewModerationStatus.Approved);

            await this.directoryEntryReviewRepository.AddAsync(entity, ct);

            if (proofAutoApproved &&
                entity.ModerationStatus == ReviewModerationStatus.Approved &&
                !string.IsNullOrWhiteSpace(entity.OrderUrl))
            {
                var tag = await this.reviewTagRepository.GetBySlugAsync(VerifiedOrderTagSlug, ct);
                if (tag is not null && tag.IsEnabled)
                {
                    await this.directoryEntryReviewRepository.EnsureTagAsync(
                        entity.DirectoryEntryReviewId,
                        tag.ReviewTagId,
                        ct);
                }
            }

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

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = input.DirectoryEntryId,
                Rating = input.Rating,
                Body = (input.Body ?? string.Empty).Trim(),
                CreateDate = DateTime.UtcNow,
                ModerationStatus = ReviewModerationStatus.Pending
            };

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

            item.OrderId = item.OrderId;
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

            ApplyOrderProof(model, orderProof);

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
        // Order proof auto-approval
        // =========================

        private async Task<bool> TryVerifyOrderUrlForEntryAsync(DirectoryEntry entry, string orderUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(orderUrl))
            {
                return false;
            }

            var entryWebsite = TryGetEntryWebsiteUri(entry);
            if (entryWebsite is null)
            {
                return false;
            }

            if (!Uri.TryCreate(orderUrl.Trim(), UriKind.Absolute, out var orderUri) ||
                (!orderUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                 !orderUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (!HostsShareDomain(entryWebsite.Host, orderUri.Host))
            {
                return false;
            }

            if (!await IsSafePublicHttpTargetAsync(orderUri, ct))
            {
                return false;
            }

            return await this.UrlReturns200Async(orderUri, ct);
        }

        private static Uri? TryGetEntryWebsiteUri(DirectoryEntry entry)
        {
            var raw = (entry.Link ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) &&
                (abs.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 abs.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                return abs;
            }

            if (Uri.TryCreate("https://" + raw.TrimStart('/'), UriKind.Absolute, out var https) &&
                (https.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 https.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                return https;
            }

            return null;
        }

        private static bool HostsShareDomain(string a, string b)
        {
            var ha = NormalizeHost(a);
            var hb = NormalizeHost(b);
            if (string.IsNullOrWhiteSpace(ha) || string.IsNullOrWhiteSpace(hb)) return false;
            if (ha.Equals(hb, StringComparison.OrdinalIgnoreCase)) return true;
            return ha.EndsWith("." + hb, StringComparison.OrdinalIgnoreCase) ||
                   hb.EndsWith("." + ha, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHost(string host)
        {
            var h = (host ?? string.Empty).Trim().TrimEnd('.');
            if (h.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) h = h.Substring(4);
            return h;
        }

        private async Task<bool> UrlReturns200Async(Uri uri, CancellationToken ct)
        {
            const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                                     "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
            try
            {
                var client = this.httpClientFactory.CreateClient(OrderProofHttpClientName);

                using var head = new HttpRequestMessage(HttpMethod.Head, uri);
                head.Headers.UserAgent.ParseAdd(userAgent);

                using var resp = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);

                // 503 from Cloudflare means the page exists but is bot-protected — treat as valid
                if (resp.StatusCode == HttpStatusCode.OK ||
                    resp.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    resp.StatusCode == HttpStatusCode.MethodNotAllowed ||
                    resp.StatusCode == HttpStatusCode.Forbidden) return true;

                if (resp.StatusCode == HttpStatusCode.NotFound ||
                    resp.StatusCode == HttpStatusCode.Gone) return false;

                // Anything else — fall through to GET
            }
            catch { return false; }

            using var get = new HttpRequestMessage(HttpMethod.Get, uri);
            get.Headers.UserAgent.ParseAdd(userAgent);
            get.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

            try
            {
                var client = this.httpClientFactory.CreateClient(OrderProofHttpClientName);
                using var resp2 = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);

                return resp2.StatusCode == HttpStatusCode.OK ||
                       resp2.StatusCode == HttpStatusCode.PartialContent ||
                       resp2.StatusCode == HttpStatusCode.ServiceUnavailable ||
                       resp2.StatusCode == HttpStatusCode.Forbidden;
            }
            catch { return false; }
        }

        private static async Task<bool> IsSafePublicHttpTargetAsync(Uri uri, CancellationToken ct)
        {
            if (uri is null) return false;
            if (uri.UserInfo?.Length > 0) return false;
            if (uri.IsLoopback) return false;

            var host = uri.DnsSafeHost;
            if (string.IsNullOrWhiteSpace(host)) return false;

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IPAddress.TryParse(host, out var ipLiteral))
            {
                return !IsPrivateOrDisallowedIp(ipLiteral);
            }

            try
            {
                var ips = await Dns.GetHostAddressesAsync(host).WaitAsync(ct);
                if (ips is null || ips.Length == 0) return false;
                foreach (var ip in ips)
                {
                    if (IsPrivateOrDisallowedIp(ip)) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPrivateOrDisallowedIp(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 10) return true;
                if (b[0] == 127) return true;
                if (b[0] == 169 && b[1] == 254) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 0) return true;
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
                return false;
            }

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
                var b = ip.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC) return true;
                if (ip.Equals(IPAddress.IPv6Loopback)) return true;
                return false;
            }

            return true;
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
                return;
            }

            if (!s.Contains(' ') &&
                s.Contains('.') &&
                Uri.TryCreate("https://" + s.TrimStart('/'), UriKind.Absolute, out var uri2) &&
                (uri2.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                 uri2.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                review.OrderUrl = uri2.ToString();
                review.OrderId = null;
                return;
            }

            review.OrderId = s;
            review.OrderUrl = null;
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