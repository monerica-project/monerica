using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Activity
{
    /// <summary>One bar on an activity chart: a week (Monday start) or calendar month, counted by type.</summary>
    public sealed class ActivityBucket
    {
        public DateTime Start { get; init; }

        public Dictionary<SiteActivityType, int> Counts { get; } = ActivityReportBuilder.Types.ToDictionary(t => t, _ => 0);

        public int Total => this.Counts.Values.Sum();
    }
}
