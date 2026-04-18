using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayPaymentEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("value")]
        public string Value { get; set; } = "0";

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("fee")]
        public string Fee { get; set; } = "0";

        [JsonProperty("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonProperty("receivedDate")]
        public long ReceivedDate { get; set; }

        [JsonIgnore]
        public DateTime ReceivedDateUtc =>
            DateTimeOffset.FromUnixTimeSeconds(ReceivedDate).UtcDateTime;
    }
}