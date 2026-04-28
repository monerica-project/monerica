using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayWebhookPayload
    {
        [JsonProperty("deliveryId")]
        public string DeliveryId { get; set; } = string.Empty;

        [JsonProperty("webhookId")]
        public string WebhookId { get; set; } = string.Empty;

        [JsonProperty("originalDeliveryId")]
        public string OriginalDeliveryId { get; set; } = string.Empty;

        [JsonProperty("isRedelivery")]
        public bool IsRedelivery { get; set; }

        // InvoiceSettled, InvoicePaymentSettled, InvoiceProcessing,
        // InvoiceReceivedPayment, InvoiceExpired, InvoiceInvalid
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("storeId")]
        public string StoreId { get; set; } = string.Empty;

        [JsonProperty("invoiceId")]
        public string InvoiceId { get; set; } = string.Empty;

        /// <summary>
        /// Present on InvoiceReceivedPayment and InvoicePaymentSettled.
        /// Contains payment.value — the XMR amount for this specific payment.
        /// Null on InvoiceSettled (aggregate event with no per-payment detail).
        /// </summary>
        [JsonProperty("payment")]
        public BtcPayWebhookPayment? Payment { get; set; }

        /// <summary>True on InvoiceExpired when partial payments were received before expiry.</summary>
        [JsonProperty("partiallyPaid")]
        public bool PartiallyPaid { get; set; }

        /// <summary>True on InvoiceSettled / InvoiceProcessing when the invoice received more than expected.</summary>
        [JsonProperty("overPaid")]
        public bool OverPaid { get; set; }

        /// <summary>True on InvoiceSettled / InvoiceInvalid when manually marked by an admin.</summary>
        [JsonProperty("manuallyMarked")]
        public bool ManuallyMarked { get; set; }

        /// <summary>True on InvoiceReceivedPayment / InvoicePaymentSettled if the payment arrived after expiry.</summary>
        [JsonProperty("afterExpiration")]
        public bool AfterExpiration { get; set; }
    }
}