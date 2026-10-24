using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    // One paid item of non-sponsorship revenue (sponsorships come from their invoices, which carry
    // the campaign dates needed to spread them over time).
    public sealed class RevenueItem
    {
        public RevenueSource Source { get; init; }

        public DateTime PaidUtc { get; init; }

        public decimal AmountUsd { get; init; }

        // XMR actually received when the payment recorded it (XMR donations, video sponsorships
        // paid in XMR). Null = only a USD value is known; the XMR view converts it at that day's price.
        public decimal? AmountXmr { get; init; }

        // True when a paid item has no USD value recorded (e.g. an old XMR donation), so its
        // amount above is 0 and the report can say how many were left out.
        public bool MissingUsd { get; init; }
    }
}
