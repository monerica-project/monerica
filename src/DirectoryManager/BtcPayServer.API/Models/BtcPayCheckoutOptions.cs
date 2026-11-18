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

        /// <summary>
        /// Minutes the invoice stays payable. Left out (store default) when null. The sponsorship
        /// checkout sets it so an invoice can never outlive the slot hold it was created under.
        /// </summary>
        [JsonProperty("expirationMinutes", NullValueHandling = NullValueHandling.Ignore)]
        public int? ExpirationMinutes { get; set; }
    }
}