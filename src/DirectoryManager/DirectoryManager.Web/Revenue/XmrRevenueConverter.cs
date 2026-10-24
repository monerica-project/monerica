using System.Reflection;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Revenue
{
    /// <summary>
    /// Re-expresses revenue inputs as the XMR actually paid, so the existing report/chart code can
    /// run unchanged (its "AmountUsd" fields then simply hold XMR). No price conversion: anything
    /// that only recorded a USD amount counts as 0 XMR and is reported separately.
    /// </summary>
    public static class XmrRevenueConverter
    {
        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

        /// <returns>XMR paid for the invoice, or 0 when it wasn't paid in XMR / not recorded.</returns>
        public static decimal InvoiceXmr(SponsoredListingInvoice inv) =>
            inv.PaidInCurrency == Currency.XMR && inv.PaidAmount > 0 ? inv.PaidAmount : 0m;

        /// <returns>Copies of the invoices whose amount is the XMR paid.</returns>
        public static List<SponsoredListingInvoice> ConvertInvoices(IEnumerable<SponsoredListingInvoice> invoices) =>
            invoices.Select(inv =>
            {
                var copy = (SponsoredListingInvoice)CloneMethod.Invoke(inv, null)!;
                copy.Amount = InvoiceXmr(inv);
                copy.Currency = Currency.USD; // the report reads the "USD" amount, which now holds XMR
                return copy;
            }).ToList();

        /// <returns>Copies of the one-time items whose amount is the XMR paid (0 when only USD is known).</returns>
        public static List<RevenueItem> ConvertItems(IEnumerable<RevenueItem> items) =>
            items.Select(i => new RevenueItem
            {
                Source = i.Source,
                PaidUtc = i.PaidUtc,
                AmountXmr = i.AmountXmr,
                AmountUsd = i.AmountXmr ?? 0m,
            }).ToList();

        /// <summary>
        /// Payments in [fromUtc, toUtc) that have no XMR amount recorded (e.g. affiliate commissions,
        /// USD-priced donations), so they are left out of the XMR view.
        /// </summary>
        /// <returns>How many and their USD total.</returns>
        public static (int Count, decimal Usd) NotPaidInXmr(
            IEnumerable<SponsoredListingInvoice> invoices,
            IEnumerable<RevenueItem> items,
            DateTime fromUtc,
            DateTime toUtc)
        {
            var inv = invoices
                .Where(i => InvoiceXmr(i) == 0m && i.CreateDate >= fromUtc && i.CreateDate < toUtc)
                .ToList();
            var one = items
                .Where(i => i.AmountXmr is null && i.PaidUtc >= fromUtc && i.PaidUtc < toUtc)
                .ToList();
            return (inv.Count + one.Count,
                    inv.Sum(i => i.Currency == Currency.USD ? i.Amount : 0m) + one.Sum(i => i.AmountUsd));
        }
    }
}
