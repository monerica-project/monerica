using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Activity;

namespace DirectoryManager.Web.Tests.Helpers
{
    // Admin activity report: everything people submit, grouped per week (Monday start) and month.
    public class ActivityReportBuilderTests
    {
        private static DateTime Utc(int y, int m, int d, int h = 0) => new (y, m, d, h, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(2026, 10, 26, 2026, 10, 26)] // Monday stays
        [InlineData(2026, 11, 1, 2026, 10, 26)]  // Sunday belongs to the week that started Monday
        [InlineData(2026, 10, 27, 2026, 10, 26)]
        [InlineData(2026, 1, 1, 2025, 12, 29)]   // week spans the new year
        public void Weeks_start_on_monday(int y, int m, int d, int ey, int em, int ed)
        {
            Assert.Equal(Utc(ey, em, ed), ActivityReportBuilder.WeekStart(Utc(y, m, d, 15)));
        }

        [Fact]
        public void Weekly_buckets_count_each_type_and_keep_empty_weeks()
        {
            var items = new[]
            {
                new SiteActivityItem(SiteActivityType.Review, Utc(2026, 10, 5, 9)),        // week of Oct 5
                new SiteActivityItem(SiteActivityType.Review, Utc(2026, 10, 11, 23)),      // Sunday, same week
                new SiteActivityItem(SiteActivityType.NewListing, Utc(2026, 10, 12)),      // next Monday
                new SiteActivityItem(SiteActivityType.ProblemReport, Utc(2026, 10, 26, 1)),
                new SiteActivityItem(SiteActivityType.Review, Utc(2026, 9, 30)),            // before the range
            };

            var weeks = ActivityReportBuilder.Build(items, Utc(2026, 10, 5), Utc(2026, 11, 1), ActivityGranularity.Week);

            Assert.Equal(new[] { Utc(2026, 10, 5), Utc(2026, 10, 12), Utc(2026, 10, 19), Utc(2026, 10, 26) }, weeks.Select(w => w.Start));
            Assert.Equal(2, weeks[0].Counts[SiteActivityType.Review]);
            Assert.Equal(1, weeks[1].Counts[SiteActivityType.NewListing]);
            Assert.Equal(0, weeks[2].Total); // empty week still present so the chart has no gaps
            Assert.Equal(1, weeks[3].Counts[SiteActivityType.ProblemReport]);
        }

        [Fact]
        public void Monthly_buckets_use_calendar_months()
        {
            var items = new[]
            {
                new SiteActivityItem(SiteActivityType.VerificationRequest, Utc(2026, 8, 31, 23)),
                new SiteActivityItem(SiteActivityType.ListingEdit, Utc(2026, 9, 1)),
                new SiteActivityItem(SiteActivityType.ReviewReply, Utc(2026, 9, 30, 12)),
            };

            var months = ActivityReportBuilder.Build(items, Utc(2026, 8, 15), Utc(2026, 10, 1), ActivityGranularity.Month);

            Assert.Equal(new[] { Utc(2026, 8, 1), Utc(2026, 9, 1) }, months.Select(m => m.Start));
            Assert.Equal(1, months[0].Total);
            Assert.Equal(2, months[1].Total);
        }

        [Fact]
        public void Totals_cover_every_type_and_ignore_items_outside_the_range()
        {
            var items = new[]
            {
                new SiteActivityItem(SiteActivityType.Review, Utc(2026, 10, 2)),
                new SiteActivityItem(SiteActivityType.Review, Utc(2026, 10, 3)),
                new SiteActivityItem(SiteActivityType.ProblemReport, Utc(2026, 11, 2)), // after the range
            };

            var totals = ActivityReportBuilder.Totals(items, Utc(2026, 10, 1), Utc(2026, 11, 1));

            Assert.Equal(ActivityReportBuilder.Types.Count, totals.Count);
            Assert.Equal(2, totals[SiteActivityType.Review]);
            Assert.Equal(0, totals[SiteActivityType.ProblemReport]);
        }

        [Fact]
        public void Trim_drops_only_leading_empty_buckets()
        {
            var items = new[] { new SiteActivityItem(SiteActivityType.Review, Utc(2026, 9, 10)) };
            var months = ActivityReportBuilder.Build(items, Utc(2026, 6, 1), Utc(2026, 11, 1), ActivityGranularity.Month);

            var trimmed = ActivityReportBuilder.TrimLeadingEmpty(months);

            Assert.Equal(Utc(2026, 9, 1), trimmed[0].Start);
            Assert.Equal(2, trimmed.Count); // September (1) and the empty October are kept
        }

        [Fact]
        public void Every_type_has_a_label_and_a_distinct_color()
        {
            var colors = ActivityReportBuilder.Types.Select(ActivityReportBuilder.Hex).ToList();
            Assert.Equal(colors.Count, colors.Distinct().Count());
            Assert.All(ActivityReportBuilder.Types, t => Assert.False(string.IsNullOrWhiteSpace(ActivityReportBuilder.Label(t))));
        }
    }
}
