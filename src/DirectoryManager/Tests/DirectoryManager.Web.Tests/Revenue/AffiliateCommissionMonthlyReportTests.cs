using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Revenue;
using R = DirectoryManager.Web.Revenue.AffiliateCommissionMonthlyReport;

namespace DirectoryManager.Web.Tests.Revenue
{
    public class AffiliateCommissionMonthlyReportTests
    {
        private static readonly DateTime Now = new (2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

        private static DateTime Utc(int y, int m, int d = 1, int h = 0) => new (y, m, d, h, 0, 0, DateTimeKind.Utc);

        private static AffiliateCommissionEarnedRow Row(int id, string name, DateTime date, decimal usd) =>
            new () { DirectoryEntryId = id, DirectoryEntryName = name, CommissionDate = date, UsdValue = usd };

        [Fact]
        public void Default_IsLast12MonthsIncludingCurrent()
        {
            var r = R.ResolveRange(null, null, null, Now, Utc(2024, 4, 26));

            Assert.Equal("last-12", r.Key);
            Assert.Equal(Utc(2025, 12), r.StartMonth);
            Assert.Equal(Utc(2026, 11), r.EndMonth);
            Assert.Equal(12, r.MonthCount);
            Assert.Equal(Utc(2026, 12), r.ToExclusive);
            Assert.Empty(r.Notices);
        }

        [Theory]
        [InlineData("this-year", 2026, 1, 2026, 11)]
        [InlineData("last-year", 2025, 1, 2025, 12)]
        [InlineData("all", 2024, 4, 2026, 11)]
        public void Presets(string key, int sy, int sm, int ey, int em)
        {
            var r = R.ResolveRange(key, "2020-01", "2020-02", Now, Utc(2024, 4, 26, 14));

            Assert.Equal(key, r.Key);
            Assert.Equal(Utc(sy, sm), r.StartMonth);
            Assert.Equal(Utc(ey, em), r.EndMonth);
        }

        [Fact]
        public void AllTime_WithNoCommissions_IsCurrentMonth()
        {
            var r = R.ResolveRange("all", null, null, Now, null);
            Assert.Equal(1, r.MonthCount);
            Assert.Equal(Utc(2026, 11), r.StartMonth);
        }

        [Fact]
        public void AllTime_LongerThanTenYears_KeepsLatest120Months()
        {
            var r = R.ResolveRange("all", null, null, Now, Utc(2010, 3));
            Assert.Equal(R.MaxMonths, r.MonthCount);
            Assert.Equal(Utc(2026, 11), r.EndMonth);
            Assert.Equal(Utc(2016, 12), r.StartMonth);
            Assert.Single(r.Notices);
        }

        [Fact]
        public void Custom_ValidRange_IsUsedAsIs()
        {
            var r = R.ResolveRange("custom", "2025-03", "2025-08", Now, null);
            Assert.Equal("custom", r.Key);
            Assert.Equal(Utc(2025, 3), r.StartMonth);
            Assert.Equal(Utc(2025, 8), r.EndMonth);
            Assert.Equal("March 2025 to August 2025", r.Label);
            Assert.Empty(r.Notices);
        }

        [Fact]
        public void Custom_MonthsWithoutRangeKey_ImplyCustom()
        {
            var r = R.ResolveRange(null, "2025-03", "2025-04", Now, null);
            Assert.Equal("custom", r.Key);
            Assert.Equal(2, r.MonthCount);
        }

        [Fact]
        public void Custom_Reversed_IsSwapped()
        {
            var r = R.ResolveRange("custom", "2025-08", "2025-03", Now, null);
            Assert.Equal(Utc(2025, 3), r.StartMonth);
            Assert.Equal(Utc(2025, 8), r.EndMonth);
            Assert.Contains(r.Notices, n => n.Contains("swapped"));
        }

        [Fact]
        public void Custom_FutureEnd_ClampedToCurrentMonth()
        {
            var r = R.ResolveRange("custom", "2026-06", "2027-05", Now, null);
            Assert.Equal(Utc(2026, 11), r.EndMonth);
            Assert.Equal(Utc(2026, 6), r.StartMonth);
            Assert.Single(r.Notices);
        }

        [Fact]
        public void Custom_FutureStartAndEnd_CollapsesToCurrentMonth()
        {
            var r = R.ResolveRange("custom", "2027-01", "2027-05", Now, null);
            Assert.Equal(Utc(2026, 11), r.StartMonth);
            Assert.Equal(Utc(2026, 11), r.EndMonth);
        }

        [Fact]
        public void Custom_SpanOverTenYears_Clamped()
        {
            var r = R.ResolveRange("custom", "2001-01", "2026-10", Now, null);
            Assert.Equal(R.MaxMonths, r.MonthCount);
            Assert.Equal(Utc(2026, 10), r.EndMonth);
            Assert.Equal(Utc(2016, 11), r.StartMonth);
        }

        [Fact]
        public void Custom_OnlyStart_EndsAtCurrentMonth_OnlyEnd_Spans12()
        {
            var a = R.ResolveRange("custom", "2026-02", null, Now, null);
            Assert.Equal(Utc(2026, 2), a.StartMonth);
            Assert.Equal(Utc(2026, 11), a.EndMonth);

            var b = R.ResolveRange("custom", null, "2025-06", Now, null);
            Assert.Equal(Utc(2024, 7), b.StartMonth);
            Assert.Equal(Utc(2025, 6), b.EndMonth);
        }

        [Theory]
        [InlineData("custom", "garbage", "13-2025")]
        [InlineData("custom", "2025-13", "")]
        [InlineData("bogus", null, null)]
        public void Invalid_FallsBackToDefault_WithNotice(string range, string? from, string? to)
        {
            var r = R.ResolveRange(range, from, to, Now, null);
            Assert.Equal("last-12", r.Key);
            Assert.Equal(12, r.MonthCount);
            Assert.NotEmpty(r.Notices);
        }

        [Fact]
        public void Build_FillsEmptyMonthsWithZero_AndBucketsByUtcMonth()
        {
            var range = R.ResolveRange("custom", "2026-01", "2026-04", Now, null);
            var rows = new[]
            {
                Row(1, "Alpha", Utc(2026, 1, 1, 0), 10m),
                Row(1, "Alpha", new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc), 5m),
                Row(2, "Beta", Utc(2026, 4, 30, 12), 20m),
                Row(2, "Beta", Utc(2025, 12, 31, 23), 999m), // before range: ignored
                Row(2, "Beta", Utc(2026, 5, 1), 999m),       // after range: ignored
            };

            var rep = R.Build(rows, range);

            Assert.Equal(4, rep.Months.Count);
            Assert.Equal(new[] { 15m, 0m, 0m, 20m }, rep.Months.Select(m => m.Usd));
            Assert.Equal(new[] { 2, 0, 0, 1 }, rep.Months.Select(m => m.Count));
            Assert.Equal(35m, rep.TotalUsd);
            Assert.Equal(3, rep.TotalCount);
            Assert.Equal(8.75m, rep.AveragePerMonthUsd);
        }

        [Fact]
        public void Build_CompanyTotals_SortedWithShareAndMonths()
        {
            var range = R.ResolveRange("custom", "2026-01", "2026-06", Now, null);
            var rows = new[]
            {
                Row(1, "Alpha", Utc(2026, 2, 3), 10m),
                Row(2, "Beta", Utc(2026, 1, 9), 30m),
                Row(2, "Beta", Utc(2026, 5, 9), 50m),
                Row(1, "Alpha", Utc(2026, 3, 3), 10m),
            };

            var rep = R.Build(rows, range);

            Assert.Equal(new[] { "Beta", "Alpha" }, rep.Companies.Select(c => c.Name));
            var beta = rep.Companies[0];
            Assert.Equal(2, beta.Count);
            Assert.Equal(80m, beta.Usd);
            Assert.Equal(80m, beta.SharePct);
            Assert.Equal(Utc(2026, 1), beta.FirstMonth);
            Assert.Equal(Utc(2026, 5), beta.LastMonth);
            Assert.Equal(20m, rep.Companies[1].SharePct);
        }

        [Fact]
        public void Build_CompanyFilter_OnlyThatListing()
        {
            var range = R.ResolveRange("custom", "2026-01", "2026-03", Now, null);
            var rows = new[]
            {
                Row(1, "Alpha", Utc(2026, 1, 3), 10m),
                Row(2, "Beta", Utc(2026, 1, 9), 30m),
                Row(1, "Alpha", Utc(2026, 3, 3), 7m),
            };

            var rep = R.Build(rows, range, companyId: 1);

            Assert.Equal(1, rep.CompanyId);
            Assert.Single(rep.Companies);
            Assert.Equal(new[] { 10m, 0m, 7m }, rep.Months.Select(m => m.Usd));
            Assert.Equal(17m, rep.TotalUsd);
            Assert.Equal(100m, rep.Companies[0].SharePct);
        }

        [Fact]
        public void Build_Empty_ZeroTotalsAndNoCompanies()
        {
            var range = R.ResolveRange(null, null, null, Now, null);
            var rep = R.Build(Array.Empty<AffiliateCommissionEarnedRow>(), range);

            Assert.Equal(12, rep.Months.Count);
            Assert.All(rep.Months, m => Assert.Equal(0m, m.Usd));
            Assert.Empty(rep.Companies);
            Assert.Equal(0m, rep.AveragePerMonthUsd);
            Assert.Empty(R.Series(rep));
        }

        [Fact]
        public void Totals_AreUsdValue_RegardlessOfPaymentCurrency()
        {
            // Rows carry only UsdValue: an XMR or BTC payment is counted at its recorded USD value,
            // the same figure /affiliatecommissionearned/totals sums.
            var range = R.ResolveRange("custom", "2026-01", "2026-01", Now, null);
            var rows = new[] { Row(1, "XMR payer", Utc(2026, 1, 2), 123.45m), Row(2, "BTC payer", Utc(2026, 1, 5), 0.55m) };

            var rep = R.Build(rows, range);

            Assert.Equal(124.00m, rep.TotalUsd);
        }

        [Fact]
        public void Series_TopListingsPlusOther_SumMatchesMonthTotals()
        {
            var range = R.ResolveRange("custom", "2026-01", "2026-02", Now, null);
            var rows = Enumerable.Range(1, 12)
                .Select(i => Row(i, "L" + i, Utc(2026, 1 + (i % 2), 5), i))
                .ToList();

            var rep = R.Build(rows, range);
            var series = R.Series(rep);

            Assert.Equal(R.MaxChartCompanies, series.Count);
            Assert.Null(series[^1].DirectoryEntryId);
            Assert.Equal("Other (5 listings)", series[^1].Name);
            Assert.Equal("L12", series[0].Name);
            for (int m = 0; m < rep.Months.Count; m++)
            {
                Assert.Equal(rep.Months[m].Usd, series.Sum(s => s.Values[m]));
            }
        }

        [Fact]
        public void MonthLabels_ArePlainEnglish()
        {
            Assert.Equal("November 2026", R.MonthLabel(Utc(2026, 11)));
            Assert.Equal("2026-11", R.MonthKey(Utc(2026, 11)));
        }

        [Fact]
        public void MonthlyBars_RendersPng_EmptyAndFull()
        {
            var range = R.ResolveRange(null, null, null, Now, null);
            Assert.True(AffiliateCommissionPlotting.MonthlyBars(R.Build(Array.Empty<AffiliateCommissionEarnedRow>(), range)).Length > 100);

            var rows = new[] { Row(1, "Alpha", Utc(2026, 3, 3), 10m), Row(2, "Beta", Utc(2026, 5, 9), 50m) };
            Assert.True(AffiliateCommissionPlotting.MonthlyBars(R.Build(rows, range)).Length > 100);
            Assert.True(AffiliateCommissionPlotting.MonthlyBars(R.Build(rows, range, 1), "Alpha").Length > 100);

            var allTime = R.ResolveRange("all", null, null, Now, Utc(2018, 1));
            Assert.True(AffiliateCommissionPlotting.MonthlyBars(R.Build(rows, allTime)).Length > 100);
        }
    }
}
