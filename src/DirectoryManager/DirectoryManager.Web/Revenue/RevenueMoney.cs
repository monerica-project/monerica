using System.Globalization;

namespace DirectoryManager.Web.Revenue
{
    /// <summary>Formats admin revenue amounts in the selected unit (USD or XMR).</summary>
    public static class RevenueMoney
    {
        private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

        /// <param name="precise">True for line items/totals (cents or 4 XMR decimals), false for compact figures.</param>
        /// <returns>"$1,234" / "$1,234.56" or "2.05 XMR" / "2.0512 XMR".</returns>
        public static string Format(decimal amount, bool xmr, bool precise = false) =>
            xmr
                ? amount.ToString(precise ? "N4" : "N2", Usd) + " XMR"
                : amount.ToString(precise ? "C2" : "C0", Usd);

        public static string Unit(bool xmr) => xmr ? "XMR" : "USD";
    }
}
