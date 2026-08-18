namespace DirectoryManager.Web.Helpers
{
    /// <summary>Per-placement-type aggregate stats over a pricing-trends range.</summary>
    public sealed class PricingTrendStat
    {
        public decimal AvgPerDay { get; set; }

        public decimal MinPerDay { get; set; }

        public decimal MaxPerDay { get; set; }

        public int DaysWithData { get; set; }

        public int PaidInvoices { get; set; }

        public long ActiveListingDays { get; set; }
    }
}
