using DirectoryManager.Web.Helpers;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    public class PaymentProcessorSettingsTests
    {
        [Fact]
        public void GetBtcPayServer_ReadsEveryValue()
        {
            var config = Build(new Dictionary<string, string?>
            {
                ["PaymentProcessors:BTCPayServer:BaseUrl"] = " https://pay.example/ ",
                ["PaymentProcessors:BTCPayServer:ApiKey"] = "key",
                ["PaymentProcessors:BTCPayServer:StoreId"] = "store",
                ["PaymentProcessors:BTCPayServer:WebhookSecret"] = "hook",
                ["PaymentProcessors:BTCPayServer:SuccessUrl"] = "https://site.example/ok",
                ["PaymentProcessors:BTCPayServer:CancelUrl"] = "https://site.example/cancel",
                ["PaymentProcessors:BTCPayServer:DefaultCurrency"] = "EUR",
                ["PaymentProcessors:BTCPayServer:ReviewDonationsStoreId"] = "review-store",
            });

            var result = PaymentProcessorSettings.GetBtcPayServer(config);

            Assert.Equal("https://pay.example/", result.BaseUrl);
            Assert.Equal("key", result.ApiKey);
            Assert.Equal("store", result.StoreId);
            Assert.Equal("hook", result.WebhookSecret);
            Assert.Equal("https://site.example/ok", result.SuccessUrl);
            Assert.Equal("https://site.example/cancel", result.CancelUrl);
            Assert.Equal("EUR", result.DefaultCurrency);
            Assert.Equal("review-store", result.ReviewDonationsStoreId);
        }

        [Fact]
        public void GetBtcPayServer_BlankCurrency_DefaultsToUsd()
        {
            var config = Build(new Dictionary<string, string?>
            {
                ["PaymentProcessors:BTCPayServer:DefaultCurrency"] = string.Empty,
            });

            Assert.Equal("USD", PaymentProcessorSettings.GetBtcPayServer(config).DefaultCurrency);
        }

        [Fact]
        public void GetNowPayments_ReadsEveryValue()
        {
            var config = Build(new Dictionary<string, string?>
            {
                ["PaymentProcessors:NOWPayments:ApiKey"] = "key",
                ["PaymentProcessors:NOWPayments:IpnSecretKey"] = "ipn",
                ["PaymentProcessors:NOWPayments:SuccessUrl"] = "ok",
                ["PaymentProcessors:NOWPayments:CancelUrl"] = "cancel",
                ["PaymentProcessors:NOWPayments:IpnCallbackUrl"] = "callback",
                ["PaymentProcessors:NOWPayments:PartiallyPaidUrl"] = "partial",
                ["PaymentProcessors:NOWPayments:PayCurrency"] = "xmr",
                ["PaymentProcessors:NOWPayments:PriceCurrency"] = "usd",
            });

            var result = PaymentProcessorSettings.GetNowPayments(config);

            Assert.Equal("key", result.ApiKey);
            Assert.Equal("ipn", result.IpnSecretKey);
            Assert.Equal("ok", result.SuccessUrl);
            Assert.Equal("cancel", result.CancelUrl);
            Assert.Equal("callback", result.IpnCallbackUrl);
            Assert.Equal("partial", result.PartiallyPaidUrl);
            Assert.Equal("xmr", result.PayCurrency);
            Assert.Equal("usd", result.PriceCurrency);
        }

        [Fact]
        public void GetNowPayments_MissingApiKey_Throws()
        {
            var config = Build(new Dictionary<string, string?>());

            Assert.Throws<InvalidOperationException>(() => PaymentProcessorSettings.GetNowPayments(config));
        }

        [Fact]
        public void GetVideoSponsorStore_NullUntilStoreAndWebhookSecretSet()
        {
            var values = new Dictionary<string, string?>
            {
                ["PaymentProcessors:BTCPayServer:BaseUrl"] = "https://pay.example/",
                ["PaymentProcessors:BTCPayServer:ApiKey"] = "main-key",
                ["PaymentProcessors:BTCPayServer:VideoSponsorStoreId"] = "video-store",
            };

            Assert.Null(PaymentProcessorSettings.GetVideoSponsorStore(Build(values)));

            values["PaymentProcessors:BTCPayServer:VideoSponsorWebhookSecret"] = "video-hook";
            var result = PaymentProcessorSettings.GetVideoSponsorStore(Build(values));

            Assert.NotNull(result);
            Assert.Equal("video-store", result!.StoreId);
            Assert.Equal("video-hook", result.WebhookSecret);
            Assert.Equal("main-key", result.ApiKey);
        }

        [Fact]
        public void GetVideoSponsorStore_OwnApiKeyWinsOverMainKey()
        {
            var config = Build(new Dictionary<string, string?>
            {
                ["PaymentProcessors:BTCPayServer:BaseUrl"] = "https://pay.example/",
                ["PaymentProcessors:BTCPayServer:ApiKey"] = "main-key",
                ["PaymentProcessors:BTCPayServer:VideoSponsorStoreId"] = "video-store",
                ["PaymentProcessors:BTCPayServer:VideoSponsorApiKey"] = "video-key",
                ["PaymentProcessors:BTCPayServer:VideoSponsorWebhookSecret"] = "video-hook",
            });

            Assert.Equal("video-key", PaymentProcessorSettings.GetVideoSponsorStore(config)!.ApiKey);
        }

        private static IConfiguration Build(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
