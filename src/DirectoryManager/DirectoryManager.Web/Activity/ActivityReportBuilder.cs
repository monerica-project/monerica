using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Activity
{
    /// <summary>
    /// Groups everything people submitted to the site into weekly or monthly buckets by type, for
    /// the admin activity report (server-rendered charts + totals table).
    /// </summary>
    public static class ActivityReportBuilder
    {
        /// <summary>Every type, in chart/legend/table order.</summary>
        public static readonly IReadOnlyList<SiteActivityType> Types = new[]
        {
            SiteActivityType.NewListing,
            SiteActivityType.ListingEdit,
            SiteActivityType.Review,
            SiteActivityType.ReviewReply,
            SiteActivityType.ProblemReport,
            SiteActivityType.VerificationRequest,
        };

        // Distinct, bright colors that read well on the dark chart background.
        private static readonly Dictionary<SiteActivityType, string> Colors = new ()
        {
            [SiteActivityType.NewListing] = "#39ff7a",          // green
            [SiteActivityType.ListingEdit] = "#22d3ff",         // cyan
            [SiteActivityType.Review] = "#ff8c1a",              // orange
            [SiteActivityType.ReviewReply] = "#f1c40f",         // yellow
            [SiteActivityType.ProblemReport] = "#ff4d6d",       // red
            [SiteActivityType.VerificationRequest] = "#b388ff", // purple
        };

        public static string Label(SiteActivityType t) => t switch
        {
            SiteActivityType.NewListing => "New listings",
            SiteActivityType.ListingEdit => "Listing edits",
            SiteActivityType.Review => "Reviews",
            SiteActivityType.ReviewReply => "Review replies",
            SiteActivityType.ProblemReport => "Problem reports",
            SiteActivityType.VerificationRequest => "Verification requests",
            _ => t.ToString(),
        };

        public static string Hex(SiteActivityType t) => Colors.TryGetValue(t, out var hex) ? hex : "#c8c8c8";

        /// <summary>Start of the week (Monday, 00:00 UTC) containing the given time.</summary>
        /// <returns>The Monday that starts the week.</returns>
        public static DateTime WeekStart(DateTime utc)
        {
            var d = utc.Date;
            int daysSinceMonday = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
            return DateTime.SpecifyKind(d.AddDays(-daysSinceMonday), DateTimeKind.Utc);
        }

        /// <summary>Start of the calendar month (00:00 UTC on the 1st) containing the given time.</summary>
        /// <returns>The first day of the month.</returns>
        public static DateTime MonthStart(DateTime utc) =>
            new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Buckets covering [fromUtc, toUtc), every week/month present even when empty (so the
        /// chart's time axis has no gaps). Items outside the range are ignored.
        /// </summary>
        /// <returns>Buckets in time order.</returns>
        public static List<ActivityBucket> Build(
            IEnumerable<SiteActivityItem> items, DateTime fromUtc, DateTime toUtc, ActivityGranularity granularity)
        {
            var buckets = new List<ActivityBucket>();
            if (toUtc <= fromUtc)
            {
                return buckets;
            }

            DateTime Floor(DateTime d) => granularity == ActivityGranularity.Week ? WeekStart(d) : MonthStart(d);
            DateTime Next(DateTime d) => granularity == ActivityGranularity.Week ? d.AddDays(7) : d.AddMonths(1);

            var index = new Dictionary<DateTime, ActivityBucket>();
            for (var start = Floor(fromUtc); start < toUtc; start = Next(start))
            {
                var bucket = new ActivityBucket { Start = start };
                buckets.Add(bucket);
                index[start] = bucket;
            }

            foreach (var item in items)
            {
                if (item.CreateDate < fromUtc || item.CreateDate >= toUtc)
                {
                    continue;
                }

                if (index.TryGetValue(Floor(item.CreateDate), out var bucket) && bucket.Counts.ContainsKey(item.Type))
                {
                    bucket.Counts[item.Type]++;
                }
            }

            return buckets;
        }

        /// <summary>Drops empty buckets before the first one with any activity (for "All time").</summary>
        /// <returns>The buckets from the first non-empty one onwards.</returns>
        public static List<ActivityBucket> TrimLeadingEmpty(List<ActivityBucket> buckets) =>
            buckets.SkipWhile(b => b.Total == 0).ToList();

        /// <summary>Count per type over the whole range.</summary>
        /// <returns>A count for every type (zero when none).</returns>
        public static Dictionary<SiteActivityType, int> Totals(IEnumerable<SiteActivityItem> items, DateTime fromUtc, DateTime toUtc)
        {
            var totals = Types.ToDictionary(t => t, _ => 0);
            foreach (var item in items)
            {
                if (item.CreateDate >= fromUtc && item.CreateDate < toUtc && totals.ContainsKey(item.Type))
                {
                    totals[item.Type]++;
                }
            }

            return totals;
        }
    }
}
