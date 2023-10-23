using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class PaymentCreationRequest
    {
        [JsonProperty("price_amount")]
        public decimal PriceAmount { get; set; }

        [JsonProperty("price_currency")]
        public string? PriceCurrency { get; set; }

        [JsonProperty("pay_amount")]
        public decimal? PayAmount { get; set; }

        [JsonProperty("pay_currency")]
        public string? PayCurrency { get; set; }

        [JsonProperty("ipn_callback_url")]
        public string? IpnCallbackUrl { get; set; }

        [JsonProperty("order_id")]
        public string? OrderId { get; set; }

        [JsonProperty("order_description")]
        public string? OrderDescription { get; set; }

        [JsonProperty("payout_address")]
        public string? PayoutAddress { get; set; }

        [JsonProperty("payout_currency")]
        public string? PayoutCurrency { get; set; }

        [JsonProperty("payout_extra_id")]
        public string? PayoutExtraId { get; set; }

        [JsonProperty("is_fixed_rate")]
        public bool? IsFixedRate { get; set; }

        [JsonProperty("is_fee_paid_by_user")]
        public bool? IsFeePaidByUser { get; set; }
    }
}
