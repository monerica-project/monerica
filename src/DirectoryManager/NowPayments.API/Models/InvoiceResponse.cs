using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class InvoiceResponse
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("token_id")]
        public string? TokenId { get; set; }

        [JsonProperty("order_id")]
        public string? OrderId { get; set; }

        [JsonProperty("order_description")]
        public string? OrderDescription { get; set; }

        [JsonProperty("price_amount")]
        public string? PriceAmount { get; set; }

        [JsonProperty("price_currency")]
        public string? PriceCurrency { get; set; }

        [JsonProperty("pay_currency")]
        public string? PayCurrency { get; set; }

        [JsonProperty("ipn_callback_url")]
        public string? IpnCallbackUrl { get; set; }

        [JsonProperty("invoice_url")]
        public string? InvoiceUrl { get; set; }

        [JsonProperty("success_url")]
        public string? SuccessUrl { get; set; }

        [JsonProperty("cancel_url")]
        public string? CancelUrl { get; set; }

        [JsonProperty("partially_paid_url")]
        public string? PartiallyPaidUrl { get; set; }

        [JsonProperty("payout_currency")]
        public string? PayoutCurrency { get; set; }

        [JsonProperty("created_at")]
        public string? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonProperty("is_fixed_rate")]
        public bool IsFixedRate { get; set; }

        [JsonProperty("is_fee_paid_by_user")]
        public bool IsFeePaidByUser { get; set; }
    }
}