using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using BtcPayServer.API.Constants;
using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
using DirectoryManager.Common.Interfaces;
using DirectoryManager.Common.Models;
using Newtonsoft.Json;

namespace BtcPayServer.API.Implementations
{
    public class BtcPayServerService : IBtcPayServerService, IRateConversionService
    {
        private readonly string storeId;
        private readonly string webhookSecret;
        private readonly HttpClient client;

        public string DefaultCurrency { get; }
        public string SuccessUrl { get; }
        public string CancelUrl { get; }

        public BtcPayServerService(BtcPayServerConfigs configs)
        {
            ArgumentNullException.ThrowIfNull(configs);

            if (string.IsNullOrWhiteSpace(configs.BaseUrl))
                throw new ArgumentNullException(nameof(configs.BaseUrl));
            if (string.IsNullOrWhiteSpace(configs.ApiKey))
                throw new ArgumentNullException(nameof(configs.ApiKey));
            if (string.IsNullOrWhiteSpace(configs.StoreId))
                throw new ArgumentNullException(nameof(configs.StoreId));
            if (string.IsNullOrWhiteSpace(configs.WebhookSecret))
                throw new ArgumentNullException(nameof(configs.WebhookSecret));

            this.storeId = configs.StoreId.Trim();
            this.webhookSecret = configs.WebhookSecret.Trim();
            this.DefaultCurrency = configs.DefaultCurrency;
            this.SuccessUrl = configs.SuccessUrl;
            this.CancelUrl = configs.CancelUrl;

            this.client = new HttpClient
            {
                BaseAddress = new Uri(configs.BaseUrl.TrimEnd('/') + "/"),
            };

            this.client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", configs.ApiKey.Trim());

            this.client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    StringConstants.JsonMediaType));
        }

        public async Task<BtcPayInvoiceResponse> CreateInvoiceAsync(
            BtcPayInvoiceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var url = $"api/v1/stores/{this.storeId}/invoices";
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(
                json,
                Encoding.UTF8,
                StringConstants.JsonMediaType);

            var response = await this.client.PostAsync(url, content)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"BTCPay invoice creation failed " +
                    $"({(int)response.StatusCode}): {body}");
            }

            return JsonConvert.DeserializeObject<BtcPayInvoiceResponse>(body)
                ?? throw new InvalidOperationException(
                    "Failed to deserialize BTCPay invoice response.");
        }

        public async Task<BtcPayInvoiceResponse> GetInvoiceAsync(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                throw new ArgumentNullException(nameof(invoiceId));

            var url = $"api/v1/stores/{this.storeId}/invoices/{invoiceId}";
            var response = await this.client.GetAsync(url).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"BTCPay get invoice failed " +
                    $"({(int)response.StatusCode}): {body}");
            }

            return JsonConvert.DeserializeObject<BtcPayInvoiceResponse>(body)
                ?? throw new InvalidOperationException(
                    "Failed to deserialize BTCPay invoice response.");
        }

        public async Task<BtcPayPaymentMethod?> GetXmrPaymentMethodAsync(
            string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                throw new ArgumentNullException(nameof(invoiceId));

            var url =
                $"api/v1/stores/{this.storeId}/invoices/{invoiceId}/payment-methods";

            var response = await this.client.GetAsync(url).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"BTCPay get invoice payment methods failed " +
                    $"({(int)response.StatusCode}): {body}");
            }

            var methods = JsonConvert.DeserializeObject<List<BtcPayPaymentMethod>>(
                body) ?? new List<BtcPayPaymentMethod>();

            return methods.FirstOrDefault(IsXmrPaymentMethod);
        }

        public async Task<BtcPayStoreRateResponse?> GetStoreRateAsync(
            string baseCurrency,
            string quoteCurrency)
        {
            if (string.IsNullOrWhiteSpace(baseCurrency))
                throw new ArgumentNullException(nameof(baseCurrency));
            if (string.IsNullOrWhiteSpace(quoteCurrency))
                throw new ArgumentNullException(nameof(quoteCurrency));

            baseCurrency = baseCurrency.Trim().ToUpperInvariant();
            quoteCurrency = quoteCurrency.Trim().ToUpperInvariant();

            var pair = $"{baseCurrency}_{quoteCurrency}";
            var url =
                $"api/v1/stores/{this.storeId}/rates" +
                $"?currencyPair={Uri.EscapeDataString(pair)}";

            var response = await this.client.GetAsync(url).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync()
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"BTCPay store rate lookup failed for {pair} " +
                    $"({(int)response.StatusCode}): {body}");
            }

            var rates = JsonConvert.DeserializeObject<List<BtcPayStoreRateResponse>>(
                body) ?? new List<BtcPayStoreRateResponse>();

            return rates.FirstOrDefault(r =>
                string.Equals(
                    r.CurrencyPair,
                    pair,
                    StringComparison.OrdinalIgnoreCase));
        }

        public async Task<decimal> GetXmrRateAsync(string quoteCurrency = "USD")
        {
            var rate = await this.GetStoreRateAsync("XMR", quoteCurrency)
                .ConfigureAwait(false);

            if (rate is null)
            {
                throw new InvalidOperationException(
                    $"No XMR rate found for quote currency '{quoteCurrency}'.");
            }

            if (rate.Errors is not null && rate.Errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"BTCPay returned rate errors for {rate.CurrencyPair}: " +
                    string.Join(", ", rate.Errors));
            }

            return rate.Rate;
        }

        public async Task<ConversionEstimate> GetEstimatedConversionAsync(
            decimal amount,
            string fromCurrency,
            string toCurrency)
        {
            if (string.IsNullOrWhiteSpace(fromCurrency))
                throw new ArgumentNullException(nameof(fromCurrency));
            if (string.IsNullOrWhiteSpace(toCurrency))
                throw new ArgumentNullException(nameof(toCurrency));

            fromCurrency = fromCurrency.Trim().ToUpperInvariant();
            toCurrency = toCurrency.Trim().ToUpperInvariant();

            if (string.Equals(fromCurrency, toCurrency, StringComparison.Ordinal))
            {
                return new ConversionEstimate
                {
                    EstimatedAmount = amount,
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                };
            }

            // Try the direct pair first (e.g. XMR_USD when converting XMR -> USD).
            var direct = await this.TryGetRateAsync(fromCurrency, toCurrency)
                .ConfigureAwait(false);

            if (direct.HasValue)
            {
                return new ConversionEstimate
                {
                    EstimatedAmount = amount * direct.Value,
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                };
            }

            // Fall back to the inverse pair (e.g. XMR_USD when converting USD -> XMR).
            var inverse = await this.TryGetRateAsync(toCurrency, fromCurrency)
                .ConfigureAwait(false);

            if (inverse.HasValue && inverse.Value > 0m)
            {
                return new ConversionEstimate
                {
                    EstimatedAmount = amount / inverse.Value,
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                };
            }

            throw new InvalidOperationException(
                $"BTCPay could not resolve a rate for " +
                $"{fromCurrency}->{toCurrency} (tried both directions).");
        }

        public bool IsWebhookValid(
            string requestBody,
            string btcPaySigHeader,
            out string errorMsg)
        {
            errorMsg = "Unknown error";

            if (string.IsNullOrWhiteSpace(btcPaySigHeader))
            {
                errorMsg = "Missing BTCPay-Sig header.";
                return false;
            }

            if (!btcPaySigHeader.StartsWith(
                    StringConstants.SigPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = "BTCPay-Sig header has unexpected format.";
                return false;
            }

            var receivedHex = btcPaySigHeader[StringConstants.SigPrefix.Length..];

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                errorMsg = "Empty request body.";
                return false;
            }

            var computed = ComputeHmacSha256(requestBody, this.webhookSecret);

            if (string.Equals(
                    computed,
                    receivedHex,
                    StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = string.Empty;
                return true;
            }

            errorMsg = "HMAC signature does not match.";
            return false;
        }

        private async Task<decimal?> TryGetRateAsync(
            string baseCurrency,
            string quoteCurrency)
        {
            try
            {
                var rate = await this.GetStoreRateAsync(baseCurrency, quoteCurrency)
                    .ConfigureAwait(false);

                if (rate is null)
                    return null;
                if (rate.Errors is not null && rate.Errors.Count > 0)
                    return null;
                if (rate.Rate <= 0m)
                    return null;

                return rate.Rate;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeHmacSha256(string data, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool IsXmrPaymentMethod(BtcPayPaymentMethod method)
        {
            if (method is null)
                return false;

            if (string.Equals(
                    method.CryptoCode,
                    "XMR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(method.PaymentMethodId) &&
                method.PaymentMethodId.StartsWith(
                    "XMR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}