using Newtonsoft.Json;

namespace BtcPayServer.API.Models
{
    public class BtcPayPaymentMethod
    {
        [JsonProperty("paymentMethodId")]
        public string PaymentMethodId { get; set; } = string.Empty;

        [JsonProperty("cryptoCode")]
        public string CryptoCode { get; set; } = string.Empty;

        [JsonProperty("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public string Amount { get; set; } = "0";

        [JsonProperty("paymentMethodPaid")]
        public string PaymentMethodPaid { get; set; } = "0";

        [JsonProperty("totalPaid")]
        public string TotalPaid { get; set; } = "0";

        [JsonProperty("rate")]
        public string Rate { get; set; } = "0";

        [JsonProperty("due")]
        public string Due { get; set; } = "0";

        [JsonProperty("payments")]
        public List<BtcPayPaymentEntry> Payments { get; set; } = new();
    }
}