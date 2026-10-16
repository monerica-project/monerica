using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Revenue
{
    public sealed class RevenueReport
    {
        public DateTime FromUtc { get; init; }

        public DateTime ToUtc { get; init; }

        public RevenueBasis Basis { get; init; }

        public List<RevenueSourceTotal> Totals { get; init; } = new ();

        public List<RevenueMonth> Months { get; init; } = new ();

        public decimal GrandTotal => this.Totals.Sum(t => t.AmountUsd);
    }
}
