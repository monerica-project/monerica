using System;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>
    /// A cumulative-income milestone: how much total income, and when you're projected to have earned it.
    /// Earliest/Latest bound the date using the optimistic/conservative scenarios.
    /// </summary>
    public class ForecastMilestoneRow
    {
        public decimal Target { get; set; }

        /// <summary>Projected date under the expected (trend) scenario.</summary>
        public DateTime? ExpectedDate { get; set; }

        /// <summary>Soonest projected date (optimistic / high band).</summary>
        public DateTime? EarliestDate { get; set; }

        /// <summary>Latest projected date (conservative / low band). Null = not reached within the horizon.</summary>
        public DateTime? LatestDate { get; set; }
    }
}
