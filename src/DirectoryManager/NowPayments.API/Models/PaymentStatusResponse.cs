using Newtonsoft.Json;

namespace NowPayments.API.Models
{
    public class PaymentStatusResponse
    {
        [JsonProperty("payment_id")]
        public long PaymentId { get; set; }

        [JsonProperty("invoice_id")]
        public long InvoiceId { get; set; }

        [JsonProperty("payment_status")]
        public string PaymentStatus { get; set; }

        [JsonProperty("pay_address")]
        public string PayAddress { get; set; }

        [JsonProperty("payin_extra_id")]
        public object PayinExtraId { get; set; }

        [JsonProperty("price_amount")]
        public decimal PriceAmount { get; set; }

        [JsonProperty("price_currency")]
        public string PriceCurrency { get; set; }

        [JsonProperty("pay_amount")]
        public decimal PayAmount { get; set; }

        [JsonProperty("actually_paid")]
        public decimal ActuallyPaid { get; set; }

        [JsonProperty("pay_currency")]
        public string PayCurrency { get; set; }

        [JsonProperty("order_id")]
        public string OrderId { get; set; }

        [JsonProperty("order_description")]
        public string OrderDescription { get; set; }

        [JsonProperty("purchase_id")]
        public long PurchaseId { get; set; }

        [JsonProperty("outcome_amount")]
        public decimal OutcomeAmount { get; set; }

        [JsonProperty("outcome_currency")]
        public string OutcomeCurrency { get; set; }

        [JsonProperty("payout_hash")]
        public object PayoutHash { get; set; }

        [JsonProperty("payin_hash")]
        public string PayinHash { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("burning_percent")]
        public object BurningPercent { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
