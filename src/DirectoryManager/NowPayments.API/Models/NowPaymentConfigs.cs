namespace NowPayments.API.Models
{
    public class NowPaymentConfigs
    {
        public NowPaymentConfigs(
            string apiKey,
            string ipnSecretKey,
            string successUrl,
            string cancelUrl,
            string ipnCallbackUrl,
            string partiallyPaidUrl,
            string payCurrency,
            string priceCurrency)
        {
            this.ApiKey = apiKey;
            this.IpnSecretKey = ipnSecretKey;
            this.SuccessUrl = successUrl;
            this.CancelUrl = cancelUrl;
            this.IpnCallbackUrl = ipnCallbackUrl;
            this.PartiallyPaidUrl = partiallyPaidUrl;
            this.PayCurrency = payCurrency;
            this.PriceCurrency = priceCurrency;
        }

        public string ApiKey { get; }
        public string IpnSecretKey { get; }
        public string SuccessUrl { get; }
        public string CancelUrl { get; }
        public string IpnCallbackUrl { get; }
        public string PartiallyPaidUrl { get; }
        public string? PayCurrency { get; set; }
        public string? PriceCurrency { get; set; }
    }
}