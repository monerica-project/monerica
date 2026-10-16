using System.Globalization;

namespace DirectoryManager.Web.Helpers
{
    // Works out a donation's USD value at the moment it is recorded, for the revenue report.
    public static class DonationUsdHelper
    {
        // USD-denominated invoice: the amount itself. XMR top-up: XMR received x the rate BTCPay
        // locked (USD per 1 XMR). Anything else: unknown (null).
        public static decimal? ToUsd(decimal? amount, string? currency, string? xmrRateUsd)
        {
            if (amount is not > 0m)
            {
                return null;
            }

            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
            }

            if (string.Equals(currency, "XMR", StringComparison.OrdinalIgnoreCase)
                && decimal.TryParse(xmrRateUsd, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                && rate > 0m)
            {
                return Math.Round(amount.Value * rate, 2, MidpointRounding.AwayFromZero);
            }

            return null;
        }
    }
}
