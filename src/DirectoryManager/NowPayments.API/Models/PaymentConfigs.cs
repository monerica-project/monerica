namespace NowPayments.API.Models
{
    public class PaymentConfigs
    {
        public PaymentConfigs(
            string apiKey,
            string ipnSecretKey,
            string successUrl,
            string cancelUrl,
            string ipnCallbackUrl,
            string partiallyPaidUrl)
        {
            this.ApiKey = apiKey;
            this.IpnSecretKey = ipnSecretKey;
            this.SuccessUrl = successUrl;
            this.CancelUrl = cancelUrl;
            this.IpnCallbackUrl = ipnCallbackUrl;
            this.PartiallyPaidUrl = partiallyPaidUrl;
        }

        public string ApiKey { get; }
        public string IpnSecretKey { get; }
        public string SuccessUrl { get; }
        public string CancelUrl { get; }
        public string IpnCallbackUrl { get; }
        public string PartiallyPaidUrl { get; }
    }
}