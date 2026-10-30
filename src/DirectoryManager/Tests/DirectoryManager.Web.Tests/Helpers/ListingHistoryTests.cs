using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ListingHistoryTests
    {
        private static readonly DateTime D0 = new (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static DirectoryEntriesAudit A(int id, DateTime at, DirectoryStatus status, string? country = "US", int? sub = 1) => new ()
        {
            DirectoryEntryId = id,
            Name = $"e{id}",
            Link = $"https://e{id}.example",
            DirectoryStatus = status,
            CountryCode = country,
            SubCategoryId = sub,
            CreateDate = at,
            UpdateDate = at,
        };

        private static ListingHistory Sample() => new (new[]
        {
            A(1, D0, DirectoryStatus.Admitted),
            A(1, D0.AddDays(10), DirectoryStatus.Removed),       // removed on day 10
            A(2, D0.AddDays(5), DirectoryStatus.Verified, "de"), // added on day 5, lowercase country
            A(3, D0, DirectoryStatus.Scam, country: null),
            A(3, D0.AddDays(3), DirectoryStatus.Scam, "FR"),     // country set later
        });

        [Fact]
        public void State_on_a_date_uses_the_newest_snapshot_up_to_the_end_of_that_day()
        {
            var h = Sample();

            Assert.Equal(new[] { 1, 3 }, h.ActiveOn(D0.Date).Select(s => s.EntryId).OrderBy(i => i));
            Assert.Equal(new[] { 1, 2, 3 }, h.ActiveOn(D0.AddDays(5).Date).Select(s => s.EntryId).OrderBy(i => i));
            Assert.Equal(new[] { 2, 3 }, h.ActiveOn(D0.AddDays(10).Date).Select(s => s.EntryId).OrderBy(i => i));
            Assert.Empty(h.ActiveOn(D0.AddDays(-1)));
        }

        [Fact]
        public void Country_comes_from_the_snapshot_in_effect_and_is_normalized()
        {
            var h = Sample();

            Assert.Null(h.ActiveOn(D0.Date).Single(s => s.EntryId == 3).CountryCode);
            Assert.Equal("FR", h.ActiveOn(D0.AddDays(4)).Single(s => s.EntryId == 3).CountryCode);
            Assert.Equal("DE", h.ActiveOn(D0.AddDays(6)).Single(s => s.EntryId == 2).CountryCode);
        }

        [Fact]
        public void Changes_over_a_range_lists_added_and_removed_listings()
        {
            var (added, removed) = Sample().Changes(D0.AddDays(1), D0.AddDays(20));

            Assert.Equal(new[] { 2 }, added);
            Assert.Equal(new[] { 1 }, removed);
        }

        [Fact]
        public void Series_covers_both_ends_and_picks_a_step_for_the_range_length()
        {
            var h = Sample();
            var series = h.ActiveSeries(D0.Date, D0.AddDays(10).Date, ReportStep.Day);

            Assert.Equal(11, series.Count);
            Assert.Equal(2, series[0].Active);
            Assert.Equal(3, series[5].Active);
            Assert.Equal(2, series[^1].Active);

            Assert.Equal(ReportStep.Day, ListingHistory.StepFor(D0, D0.AddDays(90)));
            Assert.Equal(ReportStep.Week, ListingHistory.StepFor(D0, D0.AddDays(400)));
            Assert.Equal(ReportStep.Month, ListingHistory.StepFor(D0, D0.AddYears(3)));
        }

        [Fact]
        public void Report_range_defaults_to_a_year_and_is_clamped_to_the_history()
        {
            var h = Sample();

            var (from, to) = ListingReportController.Range(null, null, h);
            Assert.Equal(DateTime.UtcNow.Date, to);
            Assert.Equal(h.Earliest, from); // a year back is before the history starts

            var (f2, t2) = ListingReportController.Range(D0.AddDays(8), D0.AddDays(2), h);
            Assert.True(f2 <= t2); // reversed dates get swapped

            var (_, t3) = ListingReportController.Range(null, DateTime.UtcNow.AddYears(1), h);
            Assert.Equal(DateTime.UtcNow.Date, t3); // never past today
        }
    }
}
