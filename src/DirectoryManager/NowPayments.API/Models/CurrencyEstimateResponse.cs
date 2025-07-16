using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class CurrencyEstimateResponse
    {
        [JsonProperty("currency_from")]
        public string CurrencyFrom { get; set; } = string.Empty;

        [JsonProperty("amount_from")]
        public decimal AmountFrom { get; set; }

        [JsonProperty("currency_to")]
        public string CurrencyTo { get; set; } = string.Empty;

        [JsonProperty("estimated_amount")]
        public decimal EstimatedAmount { get; set; }
    }
}
