using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Output of <see cref="PricingTrendCalculator"/>: the day axis, the per-placement-type daily
    /// average paid price (NaN = no paid campaign active that day), and per-type summary stats.
    /// </summary>
    public sealed class PricingTrendResult
    {
        public List<DateTime> Days { get; set; } = new ();

        /// <summary>Per placement type: one value per day; NaN = no active paid campaign that day.</summary>
        public Dictionary<SponsorshipType, double[]> DailyValues { get; } = new ();

        public Dictionary<SponsorshipType, PricingTrendStat> Stats { get; } = new ();

        public bool HasAnyData => this.Stats.Values.Any(s => s.DaysWithData > 0);
    }
}
