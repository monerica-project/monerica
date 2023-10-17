using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class PaymentRequest
    {
        [JsonProperty("price_amount")]
        public decimal PriceAmount { get; set; }

        [JsonProperty("price_currency")]
        public string PriceCurrency { get; set; }

        [JsonProperty("pay_currency", NullValueHandling = NullValueHandling.Ignore)]
        public string PayCurrency { get; set; }

        [JsonProperty("order_id", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderId { get; set; }

        [JsonProperty("order_description", NullValueHandling = NullValueHandling.Ignore)]
        public string OrderDescription { get; set; }

        [JsonProperty("ipn_callback_url", NullValueHandling = NullValueHandling.Ignore)]
        public string IpnCallbackUrl { get; set; }

        [JsonProperty("partially_paid_url", NullValueHandling = NullValueHandling.Ignore)]
        public string PartiallyPaidUrl { get; set; }

        [JsonProperty("success_url", NullValueHandling = NullValueHandling.Ignore)]
        public string SuccessUrl { get; set; }

        [JsonProperty("cancel_url", NullValueHandling = NullValueHandling.Ignore)]
        public string CancelUrl { get; set; }

        [JsonProperty("is_fixed_rate", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsFixedRate { get; set; }

        [JsonProperty("is_fee_paid_by_user", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsFeePaidByUser { get; set; }
    }
}