using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Utilities;
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

        public NowPaymentsService(PaymentConfigs paymentConfigs)
        {
            if (paymentConfigs == null)
            {
                throw new ArgumentNullException(nameof(paymentConfigs));
            }

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

            if (paymentResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize the payment response.");
            }

            return paymentResponse;
        }

        public bool IsIpnRequestValid(string requestBody, string paymentSignature, out string errorMsg)
        {
            errorMsg = "Unknown error";
            bool authOk = false;

            var receivedHmac = paymentSignature;

            if (!string.IsNullOrEmpty(receivedHmac))
            {
                var requestData = JsonConvert.DeserializeObject<Dictionary<string, object>>(requestBody);

                if (requestData != null && requestData.Any())
                {
                    var sortedRequestData = requestData.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                    var sortedRequestJson = JsonConvert.SerializeObject(sortedRequestData);

                    var hmac = CalculateHMAC(sortedRequestJson, this.ipnSecretKey);

                    if (string.Equals(hmac, receivedHmac, StringComparison.OrdinalIgnoreCase))
                    {
                        authOk = true;
                    }
                    else
                    {
                        errorMsg = "HMAC signature does not match";
                    }
                }
                else
                {
                    errorMsg = "Error reading POST data";
                }
            }
            else
            {
                errorMsg = "No HMAC signature sent.";
            }

            return authOk;
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

                    if (paymentResponse == null)
                    {
                        throw new ApiException("The API returned an unexpected response format.");
                    }

                    return paymentResponse;
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