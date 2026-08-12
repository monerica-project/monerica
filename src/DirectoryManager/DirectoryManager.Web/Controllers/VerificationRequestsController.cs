using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
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
        private readonly IBtcPayServerService btcPay;

        public VerificationRequestsController(
            IMemoryCache cache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICaptchaService captcha,
            IDirectoryEntryRepository entries,
            IVerificationRequestRepository requests,
            IBtcPayServerService btcPay)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.cache = cache;
            this.captcha = captcha;
            this.entries = entries;
            this.requests = requests;
            this.btcPay = btcPay;
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

            var request = new VerificationRequest
            {
                DirectoryEntryId = state.DirectoryEntryId,
                Comment = input.Comment.Trim(),
                Status = VerificationRequestStatus.Pending
            };
            await this.requests.AddAsync(request, ct);

            this.cache.Remove(CacheKey(flowId));

            // Land straight on the request's unique pay page (one optional "Proceed to pay"
            // button that goes to the BTCPay invoice) — no intermediate step.
            return this.RedirectToAction(nameof(this.Pay), new { token = request.PaymentToken });
        }

        [HttpGet("thanks")]
        public IActionResult Thanks(Guid? token)
        {
            this.ViewBag.PayToken = token;
            return this.View();
        }

        // Optional donation page tied to one verification request (unique GUID URL).
        [HttpGet("pay/{token:guid}")]
        public async Task<IActionResult> Pay(Guid token, CancellationToken ct)
        {
            var request = await this.requests.GetByTokenAsync(token, ct);
            if (request is null)
            {
                return this.NotFound();
            }

            var entry = request.DirectoryEntry ?? await this.entries.GetByIdAsync(request.DirectoryEntryId);

            return this.View(new VerificationPayViewModel
            {
                Token = token,
                EntryName = entry?.Name ?? "your listing",
                IsExchange = IsExchange(entry),
                AlreadyPaid = request.PaidUtc is not null,
            });
        }

        // Creates (or reuses) the top-up donation invoice on the ReviewRequests store and
        // sends the requester to the BTCPay checkout (a unique payment address per request).
        [HttpPost("pay/{token:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayPost(Guid token, CancellationToken ct)
        {
            var request = await this.requests.GetByTokenAsync(token, ct);
            if (request is null)
            {
                return this.NotFound();
            }

            var storeId = this.btcPay.ReviewRequestsStoreId;
            if (string.IsNullOrWhiteSpace(storeId))
            {
                // Not configured — send them back to the intro page rather than erroring out.
                return this.RedirectToAction(nameof(this.Pay), new { token });
            }

            // Reuse a still-valid invoice instead of minting duplicates.
            if (!string.IsNullOrWhiteSpace(request.BtcPayInvoiceId))
            {
                try
                {
                    var existing = await this.btcPay.GetInvoiceOnStoreAsync(storeId, request.BtcPayInvoiceId!);
                    if (!existing.IsExpired)
                    {
                        return this.RedirectToAction(nameof(this.PayInvoice), new { token });
                    }
                }
                catch
                {
                    // fall through and create a fresh invoice
                }
            }

            var entry = request.DirectoryEntry ?? await this.entries.GetByIdAsync(request.DirectoryEntryId);

            var invoiceRequest = new BtcPayInvoiceRequest
            {
                Amount = null, // top-up: pay-what-you-want
                Currency = "USD",
                Metadata = new Dictionary<string, object>
                {
                    // Tie the BTCPay invoice to this request's unique GUID.
                    ["orderId"] = request.PaymentToken.ToString(),
                    ["itemDesc"] = $"Monerica review-cost donation — {entry?.Name}",
                    ["verificationRequestId"] = request.VerificationRequestId,
                },
                Checkout = new BtcPayCheckoutOptions
                {
                    RedirectUrl = this.Url.Action(nameof(this.Thanks), "VerificationRequests", null, this.Request.Scheme),
                    DefaultPaymentMethod = "XMR",
                },
            };

            try
            {
                var invoice = await this.btcPay.CreateInvoiceOnStoreAsync(storeId, invoiceRequest);
                await this.requests.SetInvoiceIdAsync(request.VerificationRequestId, invoice.Id, ct);
                return this.RedirectToAction(nameof(this.PayInvoice), new { token });
            }
            catch
            {
                // BTCPay refused (e.g. the donation store has no wallet linked yet) or is down.
                // Never crash the requester — their verification request is already saved.
                this.TempData["PayError"] =
                    "Donations aren't available right now — but your verification request is submitted and in the queue. You can try again later.";
                return this.RedirectToAction(nameof(this.Pay), new { token });
            }
        }

        // No-JS Monero checkout: shows the unique payment address + QR for this request's
        // donation invoice, server-rendered (BTCPay's own hosted checkout requires JavaScript).
        [HttpGet("pay/{token:guid}/invoice")]
        public async Task<IActionResult> PayInvoice(Guid token, CancellationToken ct)
        {
            var request = await this.requests.GetByTokenAsync(token, ct);
            if (request is null || string.IsNullOrWhiteSpace(request.BtcPayInvoiceId))
            {
                return this.RedirectToAction(nameof(this.Pay), new { token });
            }

            var entry = request.DirectoryEntry ?? await this.entries.GetByIdAsync(request.DirectoryEntryId);
            var vm = new VerificationInvoiceViewModel
            {
                Token = token,
                EntryName = entry?.Name ?? "your listing",
                Paid = request.PaidUtc is not null,
            };

            try
            {
                var xmr = await this.btcPay.GetXmrPaymentMethodOnStoreAsync(
                    this.btcPay.ReviewRequestsStoreId, request.BtcPayInvoiceId!);
                if (xmr is not null && !string.IsNullOrWhiteSpace(xmr.Destination))
                {
                    vm.Address = xmr.Destination;
                    vm.QrDataUri = MoneroQrDataUri(xmr.Destination);
                }
            }
            catch
            {
                // page still renders (with a "refresh" prompt) if BTCPay is momentarily unreachable
            }

            return this.View(vm);
        }

        // Inline base64 PNG QR of a monero: URI — no separate request/JS needed.
        private static string MoneroQrDataUri(string address)
        {
            var uri = $"monero:{address}";
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(uri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var png = new QRCoder.PngByteQRCode(data);
            var bytes = png.GetGraphic(6);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        // Exchange/swap listings get the swap-fees note on the donation page.
        private static bool IsExchange(DirectoryEntry? entry)
        {
            if (entry is null)
            {
                return false;
            }

            var text = $"{entry.SubCategory?.Name} {entry.SubCategory?.Category?.Name}".ToLowerInvariant();
            return text.Contains("exchange") || text.Contains("swap");
        }

        private bool TryGetFlow(Guid flowId, out VerificationRequestFlowState state)
        {
            return this.cache.TryGetValue(CacheKey(flowId), out state!) && state.ExpiresUtc > DateTime.UtcNow;
        }
    }
}
