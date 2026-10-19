using BtcPayServer.API.Models;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Payment processor settings, read from the "PaymentProcessors" configuration section
    /// (appsettings.Production.json on the server, written by CI/deploy.sh from the
    /// git-ignored deploy-config.sh). These hold API keys and webhook secrets, so they are
    /// never stored in the database or committed.
    /// </summary>
    public static class PaymentProcessorSettings
    {
        public const string SectionName = "PaymentProcessors";
        public const string NowPaymentsSection = SectionName + ":NOWPayments";
        public const string BtcPayServerSection = SectionName + ":BTCPayServer";

        public static NowPaymentConfigs GetNowPayments(IConfiguration config)
        {
            var section = config.GetSection(NowPaymentsSection);
            var apiKey = Value(section, "ApiKey");

            if (apiKey.Length == 0)
            {
                throw new InvalidOperationException(
                    $"NOWPayments is not configured. Set the '{NowPaymentsSection}' section in appsettings.");
            }

            return new NowPaymentConfigs(
                apiKey,
                Value(section, "IpnSecretKey"),
                Value(section, "SuccessUrl"),
                Value(section, "CancelUrl"),
                Value(section, "IpnCallbackUrl"),
                Value(section, "PartiallyPaidUrl"),
                Value(section, "PayCurrency"),
                Value(section, "PriceCurrency"));
        }

        public static BtcPayServerConfigs GetBtcPayServer(IConfiguration config)
        {
            var section = config.GetSection(BtcPayServerSection);
            var defaultCurrency = Value(section, "DefaultCurrency");

            return new BtcPayServerConfigs
            {
                BaseUrl = Value(section, "BaseUrl"),
                ApiKey = Value(section, "ApiKey"),
                StoreId = Value(section, "StoreId"),
                WebhookSecret = Value(section, "WebhookSecret"),
                SuccessUrl = Value(section, "SuccessUrl"),
                CancelUrl = Value(section, "CancelUrl"),
                DefaultCurrency = defaultCurrency.Length > 0 ? defaultCurrency : "USD",
                ReviewDonationsStoreId = Value(section, "ReviewDonationsStoreId"),
                ReviewDonationsWebhookSecret = Value(section, "ReviewDonationsWebhookSecret"),
                VideoSponsorStoreId = Value(section, "VideoSponsorStoreId"),
                VideoSponsorApiKey = Value(section, "VideoSponsorApiKey"),
                VideoSponsorWebhookSecret = Value(section, "VideoSponsorWebhookSecret"),
            };
        }

        /// <summary>
        /// Settings for the separate video sponsorship store, or null until its store ID and
        /// webhook secret are set. The API key falls back to the main (account-level) key.
        /// </summary>
        public static BtcPayServerConfigs? GetVideoSponsorStore(IConfiguration config)
        {
            var main = GetBtcPayServer(config);
            var apiKey = main.VideoSponsorApiKey.Length > 0 ? main.VideoSponsorApiKey : main.ApiKey;

            if (main.BaseUrl.Length == 0
                || main.VideoSponsorStoreId.Length == 0
                || main.VideoSponsorWebhookSecret.Length == 0
                || apiKey.Length == 0)
            {
                return null;
            }

            return new BtcPayServerConfigs
            {
                BaseUrl = main.BaseUrl,
                ApiKey = apiKey,
                StoreId = main.VideoSponsorStoreId,
                WebhookSecret = main.VideoSponsorWebhookSecret,
                DefaultCurrency = main.DefaultCurrency,
            };
        }

        /// <summary>
        /// Settings for checking the donations store's webhook signatures, or null until its
        /// store ID and webhook secret are set.
        /// </summary>
        public static BtcPayServerConfigs? GetReviewDonationsStore(IConfiguration config)
        {
            var main = GetBtcPayServer(config);
            if (main.BaseUrl.Length == 0
                || main.ReviewDonationsStoreId.Length == 0
                || main.ReviewDonationsWebhookSecret.Length == 0
                || main.ApiKey.Length == 0)
            {
                return null;
            }

            return new BtcPayServerConfigs
            {
                BaseUrl = main.BaseUrl,
                ApiKey = main.ApiKey,
                StoreId = main.ReviewDonationsStoreId,
                ReviewDonationsStoreId = main.ReviewDonationsStoreId,
                WebhookSecret = main.ReviewDonationsWebhookSecret,
                DefaultCurrency = main.DefaultCurrency,
            };
        }

        private static string Value(IConfigurationSection section, string key) =>
            (section[key] ?? string.Empty).Trim();
    }
}
