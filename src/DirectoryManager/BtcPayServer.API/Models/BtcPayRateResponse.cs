using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayStoreRateResponse
    {
        [JsonProperty("currencyPair")]
        public string CurrencyPair { get; set; } = string.Empty;

        [JsonProperty("errors")]
        public List<string> Errors { get; set; } = new();

        [JsonProperty("rate")]
        public decimal Rate { get; set; }
    }
}