using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayCheckoutOptions
    {
        [JsonProperty("redirectURL")]
        public string? RedirectUrl { get; set; }

        [JsonProperty("redirectAutomatically")]
        public bool RedirectAutomatically { get; set; } = true;

        [JsonProperty("defaultPaymentMethod")]
        public string? DefaultPaymentMethod { get; set; }
    }
}