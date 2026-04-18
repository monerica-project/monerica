using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class PaymentResponse
    {
        [JsonProperty("payment_id")]
        public string? PaymentId { get; set; }

        [JsonProperty("payment_status")]
        public string? PaymentStatus { get; set; }

        [JsonProperty("pay_address")]
        public string? PayAddress { get; set; }

        [JsonProperty("price_amount")]
        public float PriceAmount { get; set; }

        [JsonProperty("price_currency")]
        public string? PriceCurrency { get; set; }

        [JsonProperty("pay_amount")]
        public float PayAmount { get; set; }

        [JsonProperty("pay_currency")]
        public string? PayCurrency { get; set; }

        [JsonProperty("order_id")]
        public string? OrderId { get; set; }

        [JsonProperty("order_description")]
        public string? OrderDescription { get; set; }

        [JsonProperty("ipn_callback_url")]
        public string? IpnCallbackUrl { get; set; }

        [JsonProperty("created_at")]
        public string? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonProperty("purchase_id")]
        public string? PurchaseId { get; set; }

        [JsonProperty("amount_received")]
        public float AmountReceived { get; set; }

        [JsonProperty("payin_extra_id")]
        public string? PayinExtraId { get; set; }

        [JsonProperty("smart_contract")]
        public string? SmartContract { get; set; }

        [JsonProperty("network")]
        public string? Network { get; set; }

        [JsonProperty("network_precision")]
        public string? NetworkPrecision { get; set; }

        [JsonProperty("time_limit")]
        public string? TimeLimit { get; set; }

        [JsonProperty("expiration_estimate_date")]
        public string? ExpirationEstimateDate { get; set; }

        [JsonProperty("is_fixed_rate")]
        public string? IsFixedRate { get; set; }

        [JsonProperty("is_fee_paid_by_user")]
        public string? IsFeePaidByUser { get; set; }

        [JsonProperty("valid_until")]
        public string? ValidUntil { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("redirectData: redirect_url")]
        public string? RedirectUrl { get; set; }
    }
}