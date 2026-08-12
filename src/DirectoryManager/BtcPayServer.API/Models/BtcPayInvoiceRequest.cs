using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayInvoiceRequest
    {
        // Nullable + omit-when-null so a "top-up" (pay-what-you-want) invoice can be
        // created by leaving Amount unset. Fixed-price invoices still send a value.
        [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
        public string? Amount { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonProperty("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonProperty("checkout")]
        public BtcPayCheckoutOptions? Checkout { get; set; }
    }
}