namespace DirectoryManager.Utilities.Helpers
{
    public static class CurrencyFormatter
    {
        public static string Format(decimal value, decimal conversionRate, string currency, bool showCurrencyName)
        {
            switch (currency.ToLowerInvariant())
            {
                case "xmr":
                    if (showCurrencyName)
                    {
                        return string.Format("{0:F7} XMR", value * conversionRate);
                    }

                    return string.Format("{0:F7}", value * conversionRate);
                case "btc":
                    if (showCurrencyName)
                    {
                        return string.Format("{0:F8} BTC", value * conversionRate);
                    }

                    return string.Format("{0:F8}", value * conversionRate);
                case "eth":
                    if (showCurrencyName)
                    {
                        return string.Format("{0:F7} ETH", value * conversionRate);
                    }

                    return string.Format("{0:F7}", value * conversionRate);
                case "usd":
                    return value.ToString("C");
                default:
                    return value.ToString("C");
            }
        }
    }
}