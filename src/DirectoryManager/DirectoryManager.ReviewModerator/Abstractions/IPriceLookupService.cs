namespace DirectoryManager.ReviewModerator.Abstractions
{
    /// <summary>
    /// Looks up the USD value of a crypto amount. The concrete implementation will call
    /// the (forthcoming) MoneroMarketCap price API. Until that exists, the stub returns
    /// <see cref="PriceLookupResult.Unavailable"/>, in which case the worker still approves
    /// completed orders but skips the money-band tag (completion is the hard gate; the USD
    /// band is best-effort).
    /// </summary>
    public interface IPriceLookupService
    {
        /// <summary>
        /// Resolve the USD value of <paramref name="amount"/> of <paramref name="assetSymbol"/>.
        /// </summary>
        /// <param name="assetSymbol">Ticker as parsed from the proof (e.g. "BTC", "ETH", "XMR", "USDT").</param>
        /// <param name="amount">Quantity of that asset.</param>
        /// <param name="asOfUtc">
        /// Price-as-of time. Pass the order's completion time for historical accuracy;
        /// pass null to use the latest spot price.
        /// </param>
        Task<PriceLookupResult> GetUsdValueAsync(
            string assetSymbol,
            decimal amount,
            DateTime? asOfUtc,
            CancellationToken ct = default);
    }

    public sealed class PriceLookupResult
    {
        public bool Available { get; init; }

        public decimal? UsdValue { get; init; }

        public string? Note { get; init; }

        public static PriceLookupResult Found(decimal usd) =>
            new () { Available = true, UsdValue = usd };

        public static PriceLookupResult Unavailable(string note) =>
            new () { Available = false, Note = note };
    }
}
