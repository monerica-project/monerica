using NowPayments.API.Models;

namespace DirectoryManager.Web.Providers
{
    public class NowPaymentsConfigProvider
    {
        private readonly IConfiguration configuration;

        public NowPaymentsConfigProvider(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public PaymentConfigs GetConfigs()
        {
            var apiKey = this.configuration.GetSection("NowPayments:ApiKey")?.Value ?? string.Empty;
            this.ValidateConfigValue(apiKey, "NowPayments:ApiKey", "The API key for NowPayments is not configured or is empty.");

            var ipnSecretKey = this.configuration.GetSection("NowPayments:IpnSecretKey")?.Value ?? string.Empty;
            this.ValidateConfigValue(ipnSecretKey, "NowPayments:IpnSecretKey", "The IPN secret key for NowPayments is not configured or is empty.");

            var successUrl = this.configuration.GetSection("NowPayments:SuccessUrl")?.Value ?? string.Empty;
            this.ValidateConfigValue(successUrl, "NowPayments:SuccessUrl", "The success URL for NowPayments is not configured or is empty.");

            var cancelUrl = this.configuration.GetSection("NowPayments:CancelUrl")?.Value ?? string.Empty;
            this.ValidateConfigValue(cancelUrl, "NowPayments:CancelUrl", "The cancel URL for NowPayments is not configured or is empty.");

            var ipnCallbackUrl = this.configuration.GetSection("NowPayments:IpnCallbackUrl")?.Value ?? string.Empty;
            this.ValidateConfigValue(ipnCallbackUrl, "NowPayments:IpnCallbackUrl", "The IPN callback URL for NowPayments is not configured or is empty.");

            var partiallyPaidUrl = this.configuration.GetSection("NowPayments:PartiallyPaidUrl")?.Value ?? string.Empty;
            this.ValidateConfigValue(partiallyPaidUrl, "NowPayments:PartiallyPaidUrl", "The partially paid URL for NowPayments is not configured or is empty.");

            return new PaymentConfigs(apiKey, ipnSecretKey, successUrl, cancelUrl, ipnCallbackUrl, partiallyPaidUrl);
        }

        private void ValidateConfigValue(string value, string configKey, string errorMessage)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
