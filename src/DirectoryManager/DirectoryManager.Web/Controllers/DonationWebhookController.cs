using BtcPayServer.API.Implementations;
using BtcPayServer.API.Models;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// BTCPay webhook for the donations store (optional donations on submissions, verification
    /// requests and problem reports). BTCPay calls this when a payment arrives, so pages never
    /// have to ask BTCPay themselves.
    /// </summary>
    [AllowAnonymous]
    public class DonationWebhookController : Controller
    {
        // Events that can mean money arrived. Created/expired-without-payment are ignored.
        private static readonly HashSet<string> PaymentEvents = new (StringComparer.OrdinalIgnoreCase)
        {
            "InvoiceReceivedPayment", "InvoiceProcessing", "InvoicePaymentSettled", "InvoiceSettled", "InvoiceExpired",
        };

        private readonly IConfiguration config;
        private readonly IDonationPaymentService donations;
        private readonly ILogger<DonationWebhookController> logger;

        public DonationWebhookController(IConfiguration config, IDonationPaymentService donations, ILogger<DonationWebhookController> logger)
        {
            this.config = config;
            this.donations = donations;
            this.logger = logger;
        }

        [HttpPost("donations/btcpay-callback")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BtcPayCallback(CancellationToken ct)
        {
            var store = PaymentProcessorSettings.GetReviewDonationsStore(this.config);
            if (store is null)
            {
                return this.Ok(); // donations webhook not configured
            }

            string rawBody;
            using (var reader = new StreamReader(this.Request.Body, System.Text.Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync(ct);
            }

            var signature = this.Request.Headers["BTCPay-Sig"].FirstOrDefault() ?? string.Empty;
            if (!new BtcPayServerService(store).IsWebhookValid(rawBody, signature, out var signatureError))
            {
                this.logger.LogWarning("Donations webhook signature invalid: {Error}", signatureError);
                return this.Ok();
            }

            BtcPayWebhookPayload? payload;
            try
            {
                payload = JsonConvert.DeserializeObject<BtcPayWebhookPayload>(rawBody);
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "Donations webhook could not be read.");
                return this.Ok();
            }

            if (payload is null
                || string.IsNullOrWhiteSpace(payload.InvoiceId)
                || !PaymentEvents.Contains(payload.Type)
                || (payload.Type == "InvoiceExpired" && !payload.PartiallyPaid))
            {
                return this.Ok();
            }

            try
            {
                await this.donations.RecordPaymentAsync(payload.InvoiceId, ct);
            }
            catch (Exception ex)
            {
                // 500 makes BTCPay redeliver later (e.g. if BTCPay's own API hiccuped).
                this.logger.LogWarning(ex, "Donations webhook: recording payment for {Id} failed.", payload.InvoiceId);
                return this.StatusCode(500);
            }

            return this.Ok();
        }
    }
}
