using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Revenue
{
    public sealed class RevenueSourceTotal
    {
        public RevenueSource Source { get; init; }

        public decimal AmountUsd { get; init; }

        // Payments in the period (for earned sponsorships: invoices that earned anything in it).
        public int Count { get; init; }

        public decimal SharePct { get; init; }

        public int MissingUsdCount { get; init; }
    }
}
