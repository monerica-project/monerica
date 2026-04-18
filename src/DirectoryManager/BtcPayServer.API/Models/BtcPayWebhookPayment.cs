using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    /// <summary>
    /// The nested "payment" object present on InvoiceReceivedPayment and
    /// InvoicePaymentSettled webhook events. Contains the actual XMR amount.
    /// </summary>
    public class BtcPayWebhookPayment
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("receivedDate")]
        public long ReceivedDate { get; set; }

        /// <summary>
        /// The XMR amount of this specific payment, e.g. "0.46730000".
        /// Always populated on InvoiceReceivedPayment and InvoicePaymentSettled.
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; } = "0";

        [JsonProperty("fee")]
        public string Fee { get; set; } = "0";

        /// <summary>Settled, Processing, Invalid, etc.</summary>
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("destination")]
        public string Destination { get; set; } = string.Empty;
    }

}
