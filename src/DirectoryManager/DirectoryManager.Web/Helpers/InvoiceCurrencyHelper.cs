using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Web.Helpers
{
    public static class InvoiceCurrencyHelper
    {
        /// <summary>
        /// True if the invoice has any amount expressed in the requested currency.
        /// </summary>
        public static bool MatchesCurrency(this SponsoredListingInvoice inv, Currency currency) =>
            inv.Currency == currency || inv.PaidInCurrency == currency;

        /// <summary>
        /// Returns the invoice amount denominated in the requested currency (0 if not applicable).
        /// If the requested currency equals the invoice's requested Currency, returns Amount.
        /// Else if it equals PaidInCurrency, returns PaidAmount.
        /// Otherwise 0.
        /// </summary>
        public static decimal AmountIn(this SponsoredListingInvoice inv, Currency currency)
        {
            if (inv.Currency == currency) return inv.Amount;
            if (inv.PaidInCurrency == currency) return inv.PaidAmount;
            return 0m;
        }
    }
}