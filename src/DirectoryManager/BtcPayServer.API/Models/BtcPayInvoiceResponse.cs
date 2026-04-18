using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayInvoiceResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("additionalStatus")]
        public string AdditionalStatus { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public string Amount { get; set; } = "0";

        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonProperty("createdTime")]
        public long CreatedTime { get; set; }

        [JsonProperty("expirationTime")]
        public long ExpirationTime { get; set; }

        [JsonProperty("monitoringExpiration")]
        public long MonitoringExpiration { get; set; }

        [JsonProperty("checkoutLink")]
        public string CheckoutLink { get; set; } = string.Empty;

        [JsonProperty("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonIgnore]
        public DateTime ExpirationTimeUtc =>
            DateTimeOffset.FromUnixTimeSeconds(ExpirationTime).UtcDateTime;

        [JsonIgnore]
        public DateTime CreatedTimeUtc =>
            DateTimeOffset.FromUnixTimeSeconds(CreatedTime).UtcDateTime;

        [JsonIgnore]
        public bool IsSettled => Status is "Settled" or "Complete";

        [JsonIgnore]
        public bool IsExpired => Status is "Expired" or "Invalid";
    }
}
