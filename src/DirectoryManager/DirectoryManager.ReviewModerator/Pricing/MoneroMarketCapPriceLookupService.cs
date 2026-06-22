using DirectoryManager.ReviewModerator.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.ReviewModerator.Pricing
{
    /// <summary>
    /// Placeholder price source. The MoneroMarketCap price API does not exist yet, so this
    /// returns <see cref="PriceLookupResult.Unavailable"/> for everything. Wire the real
    /// endpoint here later (config key: "MoneroMarketCap:PriceApiBaseUrl"); the rest of the
    /// pipeline already handles "price available" vs "not available" and will start applying
    /// money-band tags automatically once this returns real values.
    /// </summary>
    public sealed class MoneroMarketCapPriceLookupService : IPriceLookupService
    {
        private readonly string? baseUrl;

        public MoneroMarketCapPriceLookupService(IConfiguration config)
        {
            this.baseUrl = config["MoneroMarketCap:PriceApiBaseUrl"];
        }

        public Task<PriceLookupResult> GetUsdValueAsync(
            string assetSymbol,
            decimal amount,
            DateTime? asOfUtc,
            CancellationToken ct = default)
        {
            // TODO: when MoneroMarketCap exposes the price API, replace this body with a call to
            //   GET {baseUrl}/price?symbol={assetSymbol}&at={asOfUtc:o}   (or whatever shape lands)
            // returning PriceLookupResult.Found(amount * unitUsd).
            if (string.IsNullOrWhiteSpace(this.baseUrl))
            {
                return Task.FromResult(
                    PriceLookupResult.Unavailable("MoneroMarketCap price API not configured yet."));
            }

            return Task.FromResult(
                PriceLookupResult.Unavailable("MoneroMarketCap price API not implemented yet."));
        }
    }
}
