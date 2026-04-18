using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayInvoiceRequest
    {
        [JsonProperty("amount")]
        public string Amount { get; set; } = string.Empty;

        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonProperty("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonProperty("checkout")]
        public BtcPayCheckoutOptions? Checkout { get; set; }
    }
}