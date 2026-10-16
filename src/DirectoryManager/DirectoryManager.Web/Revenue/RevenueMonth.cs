using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Revenue
{
    public sealed class RevenueMonth
    {
        public DateTime Month { get; init; }

        public Dictionary<RevenueSource, decimal> BySource { get; init; } = new ();

        public decimal Total => this.BySource.Values.Sum();
    }
}
