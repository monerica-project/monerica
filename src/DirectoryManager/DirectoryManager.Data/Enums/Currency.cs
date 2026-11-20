namespace DirectoryManager.Data.Enums
{
    public enum Currency
    {
        Unknown = 0,
        XMR = 1,
        USD = 2,
        BTC = 3,

        /// <summary>Tether on the Ethereum network. Used for affiliate commission payouts only.</summary>
        UsdtEth = 4
    }

    public static class CurrencyExtensions
    {
        /// <summary>The name shown to people: the code for most, "USDT (ETH)" for Tether on Ethereum.</summary>
        public static string ToLabel(this Currency currency) =>
            currency == Currency.UsdtEth ? "USDT (ETH)" : currency.ToString();
    }
}