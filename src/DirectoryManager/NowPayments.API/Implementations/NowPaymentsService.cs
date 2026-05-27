using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Utilities.Helpers;
using Newtonsoft.Json;
using NowPayments.API.Constants;
using NowPayments.API.Enums;
using NowPayments.API.Exceptions;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace NowPayments.API.Implementations
{
    public class NowPaymentsService : INowPaymentsService
    {
        private readonly string apiKey;
        private readonly string ipnSecretKey;
        private readonly string successUrl;
        private readonly string cancelUrl;
        private readonly string ipnCallbackUrl;
        private readonly string partiallyPaidUrl;
        private readonly HttpClient client;

        public NowPaymentsService(NowPaymentConfigs paymentConfigs)
        {
            ArgumentNullException.ThrowIfNull(paymentConfigs);

            this.apiKey = paymentConfigs.ApiKey
                ?? throw new ArgumentNullException(nameof(paymentConfigs.ApiKey));
            this.ipnSecretKey = paymentConfigs.IpnSecretKey
                ?? throw new ArgumentNullException(nameof(paymentConfigs.IpnSecretKey));

            if (string.IsNullOrWhiteSpace(paymentConfigs.PayCurrency))
            {
                throw new ArgumentNullException(nameof(paymentConfigs.PayCurrency));
            }
            else
            {
                this.PayCurrency = EnumHelper.ParseStringToEnum<Currency>(paymentConfigs.PayCurrency).ToString();
            }

            if (string.IsNullOrWhiteSpace(paymentConfigs.PriceCurrency))
            {
                throw new ArgumentNullException(nameof(paymentConfigs.PriceCurrency));
            }
            else
            {
                this.PriceCurrency = EnumHelper.ParseStringToEnum<Currency>(paymentConfigs.PriceCurrency).ToString();
            }

            this.successUrl = paymentConfigs.SuccessUrl;
            this.cancelUrl = paymentConfigs.CancelUrl;
            this.ipnCallbackUrl = paymentConfigs.IpnCallbackUrl;
            this.partiallyPaidUrl = paymentConfigs.PartiallyPaidUrl;
            this.client = new HttpClient();
            this.InitializeClient();
        }

        public string PayCurrency { get; private set; }
        public string PriceCurrency { get; private set; }

        public async Task<PaymentResponse> CreatePaymentAsync(PaymentCreationRequest paymentRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, StringConstants.ApiPaymentUrl);
            var contentJson = JsonConvert.SerializeObject(paymentRequest);
            var content = new StringContent(contentJson, Encoding.UTF8, StringConstants.JsonMediaType);
            request.Content = content;

            var response = await this.client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var paymentResponse = JsonConvert.DeserializeObject<PaymentResponse>(responseContent);

            return paymentResponse ?? throw new InvalidOperationException("Failed to deserialize the payment response.");
        }

        public bool IsIpnRequestValid(string requestBody, string paymentSignature, out string errorMsg)
        {
            errorMsg = "Unknown error";

            if (string.IsNullOrEmpty(paymentSignature))
            {
                errorMsg = "No HMAC signature sent.";
                return false;
            }

            var requestData = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestBody);
            if (requestData == null || requestData.Count == 0)
            {
                errorMsg = "Error reading POST data";
                return false;
            }

            var sortedRequestData = requestData.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            var sortedRequestJson = JsonConvert.SerializeObject(sortedRequestData);

            var computed = CalculateHMAC(sortedRequestJson, this.ipnSecretKey);

            if (FixedTimeHexEquals(computed, paymentSignature))
            {
                errorMsg = string.Empty;
                return true;
            }

            errorMsg = "HMAC signature does not match";
            return false;
        }

        /// <summary>
        /// Constant-time comparison of two hex strings, case-insensitive.
        /// </summary>
        private static bool FixedTimeHexEquals(string a, string b)
        {
            if (a is null || b is null || a.Length != b.Length || (a.Length % 2) != 0)
            {
                return false;
            }

            try
            {
                var aBytes = Convert.FromHexString(a);
                var bBytes = Convert.FromHexString(b);
                return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public async Task<PaymentStatusResponse> GetPaymentStatusAsync(string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId))
            {
                throw new ArgumentNullException(nameof(paymentId), "Payment ID cannot be null or empty.");
            }

            var url = $"{StringConstants.ApiPaymentUrl}/{paymentId}";
            var response = await this.client.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var paymentStatusResponse = JsonConvert.DeserializeObject<PaymentStatusResponse>(responseContent);

            if (paymentStatusResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize the payment status response.");
            }

            return paymentStatusResponse;
        }

        public async Task<InvoiceResponse> CreateInvoice(PaymentRequest request)
        {
            try
            {
                var response = await this.client.PostAsync(
                    StringConstants.ApiInvoiceUrl,
                    new StringContent(
                        JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        StringConstants.JsonMediaType));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var paymentResponse = JsonConvert.DeserializeObject<InvoiceResponse>(content);

                    return paymentResponse ?? throw new ApiException("The API returned an unexpected response format.");
                }
                else
                {
                    throw new ApiException($"API request failed with status code: {response.StatusCode}. Error: {content}");
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                throw new ApiException("There was an error sending the request to the API.", httpRequestException);
            }
            catch (Exception ex)
            {
                throw new ApiException("An unexpected error occurred.", ex);
            }
        }

        public void SetDefaultUrls(PaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CancelUrl))
            {
                request.CancelUrl = this.cancelUrl;
            }

            if (string.IsNullOrWhiteSpace(request.SuccessUrl))
            {
                request.SuccessUrl = this.successUrl;
            }

            if (string.IsNullOrWhiteSpace(request.IpnCallbackUrl))
            {
                request.IpnCallbackUrl = this.ipnCallbackUrl;
            }

            if (string.IsNullOrWhiteSpace(request.PartiallyPaidUrl))
            {
                request.PartiallyPaidUrl = this.partiallyPaidUrl;
            }
        }

        public async Task<CurrencyEstimateResponse> GetEstimatedConversionAsync(decimal amount, string fromCurrency, string toCurrency)
        {
            string url = $"{StringConstants.ApiUrl}/estimate?amount={amount:0.0000}&currency_from={fromCurrency}&currency_to={toCurrency}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", this.apiKey);

            try
            {
                var response = await this.client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var estimate = JsonConvert.DeserializeObject<CurrencyEstimateResponse>(content);

                return estimate;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse estimate response.", ex.InnerException);
            }
        }

        private static string CalculateHMAC(string data, string secret)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void InitializeClient()
        {
            this.client.DefaultRequestHeaders.Add(StringConstants.HeaderNameApiKey, this.apiKey);
            this.client.DefaultRequestHeaders.Accept.Clear();
            this.client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(StringConstants.JsonMediaType));
        }
    }
}