using DirectoryManager.Web.Constants;
using Microsoft.Extensions.Caching.Memory;
using NowPayments.API.Interfaces;

namespace DirectoryManager.Web.Helpers
{
    public static class CurrencyConversionHelper
    {
        private const Data.Enums.Currency MainCurrency = Data.Enums.Currency.USD;
        private const int ExpirationTimeInMinutes = 5;

        public static async Task<(bool showConverted, decimal conversionRate, string selectedCurrency)> GetConversionContextAsync(
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            INowPaymentsService nowPaymentsService)
        {
            var request = httpContextAccessor.HttpContext?.Request;
            string selectedCurrency = Data.Enums.Currency.XMR.ToString();

            bool showConverted = !selectedCurrency.Equals(MainCurrency.ToString(), StringComparison.OrdinalIgnoreCase);
            decimal conversionRate = 1m;

            if (showConverted)
            {
                string cacheKey = $"{Constants.StringConstants.CacheKeyPrefixConversion}_{selectedCurrency}";

                if (!memoryCache.TryGetValue(cacheKey, out conversionRate))
                {
                    try
                    {
                        var estimate = await nowPaymentsService.GetEstimatedConversionAsync(
                            1m,
                            MainCurrency.ToString(),
                            selectedCurrency);

                        conversionRate = estimate.EstimatedAmount;

                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetAbsoluteExpiration(TimeSpan.FromMinutes(ExpirationTimeInMinutes));

                        memoryCache.Set(cacheKey, conversionRate, cacheEntryOptions);
                    }
                    catch
                    {
                        // If conversion fails (e.g. currency not supported), fallback to USD
                        showConverted = false;
                        conversionRate = 1m;
                        selectedCurrency = MainCurrency.ToString();
                    }
                }
            }

            return (showConverted, conversionRate, selectedCurrency);
        }
    }
}
