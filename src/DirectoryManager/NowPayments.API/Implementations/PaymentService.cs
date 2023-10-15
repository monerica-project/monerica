using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using NowPayments.API.Constants;
using NowPayments.API.Exceptions;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace NowPayments.API.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly string apiKey;
        private readonly string ipnSecretKey;
        private readonly HttpClient client;

        public PaymentService(string apiKey, string ipnSecretKey)
        {
            this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            this.ipnSecretKey = ipnSecretKey ?? throw new ArgumentNullException(nameof(ipnSecretKey));
            this.client = new HttpClient();
            this.InitializeClient();
        }

        private void InitializeClient()
        {
            this.client.DefaultRequestHeaders.Add("x-api-key", this.apiKey);
            this.client.DefaultRequestHeaders.Accept.Clear();
            this.client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(StringConstants.JsonMediaType));
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


        public async Task<PaymentResponse> CreateInvoice(PaymentRequest request)
        {
            try
            {
                var response = await this.client.PostAsync(
                    StringConstants.ApiUrl,
                    new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, StringConstants.JsonMediaType));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var paymentResponse = JsonConvert.DeserializeObject<PaymentResponse>(content);

                    if (paymentResponse == null)
                    {
                        throw new ApiException("The API returned an unexpected response format.");
                    }

                    return paymentResponse;
                }
                else
                {
                    // Log the error content for debugging purposes or throw it as part of the exception.
                    // This assumes the API provides a meaningful error message in its response.
                    throw new ApiException($"API request failed with status code: {response.StatusCode}. Error: {content}");
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                // Handle exceptions related to the request itself (e.g., connectivity issues, timeouts, etc.)
                throw new ApiException("There was an error sending the request to the API.", httpRequestException);
            }
            catch (Exception ex)
            {
                // Handle other potential exceptions
                throw new ApiException("An unexpected error occurred.", ex);
            }
        }

        private static string CalculateHMAC(string data, string secret)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}