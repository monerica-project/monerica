using DirectoryManager.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Helpers
{
    public static class CurrencyConversionHelper
    {
        private const Data.Enums.Currency MainCurrency = Data.Enums.Currency.USD;
        private const int ExpirationTimeInMinutes = 5;
        private const int FailureCacheSeconds = 60;

        public static async Task<(bool showConverted, decimal conversionRate, string selectedCurrency)> GetConversionContextAsync(
            IHttpContextAccessor httpContextAccessor,
            IMemoryCache memoryCache,
            IRateConversionService rateConversionService,
            bool bypassCache = false)
        {
            var request = httpContextAccessor.HttpContext?.Request;
            string selectedCurrency = Data.Enums.Currency.XMR.ToString();

            bool showConverted = !selectedCurrency.Equals(
                MainCurrency.ToString(),
                StringComparison.OrdinalIgnoreCase);

            decimal conversionRate = 1m;

            if (!showConverted)
            {
                return (false, 1m, MainCurrency.ToString());
            }

            string cacheKey = $"{Constants.StringConstants.CacheKeyPrefixConversion}_{selectedCurrency}";
            string failKey = $"{cacheKey}_fail";

            if (memoryCache.TryGetValue(failKey, out bool _))
            {
                // Recent failure — don't hammer the provider.
                return (false, 1m, MainCurrency.ToString());
            }

            if (!bypassCache && memoryCache.TryGetValue(cacheKey, out conversionRate))
            {
                return (true, conversionRate, selectedCurrency);
            }

            try
            {
                var estimate = await rateConversionService.GetEstimatedConversionAsync(
                    1m,
                    MainCurrency.ToString(),
                    selectedCurrency);

                conversionRate = estimate.EstimatedAmount;

                if (conversionRate <= 0m)
                {
                    throw new InvalidOperationException(
                        "Rate provider returned a non-positive rate.");
                }

                memoryCache.Set(
                    cacheKey,
                    conversionRate,
                    new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(ExpirationTimeInMinutes)));

                return (true, conversionRate, selectedCurrency);
            }
            catch
            {
                memoryCache.Set(
                    failKey,
                    true,
                    new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(FailureCacheSeconds)));

                return (false, 1m, MainCurrency.ToString());
            }
        }
    }
}