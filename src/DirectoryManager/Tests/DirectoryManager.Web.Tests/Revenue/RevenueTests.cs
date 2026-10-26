using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Tests.Revenue
{
    public class RevenueTests
    {
        private static readonly DateTime Now = new (2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        // ---------------- accrual ----------------
        [Fact]
        public void YearPaidUpFront_EarnsOneDayAtATime()
        {
            var inv = Invoice(start: new DateTime(2026, 1, 1), days: 365, amount: 3650m);

            Assert.Equal(10m, SponsorAccrual.DailyRate(inv));

            // January (31 days) earns 31/365 of the price, not all of it.
            var jan = SponsorAccrual.EarnedBetween(inv, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), Now);
            Assert.Equal(310m, jan);

            // Days after "now" haven't been earned yet.
            var future = SponsorAccrual.EarnedBetween(inv, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1), Now);
            Assert.Equal(0m, future);
        }

        [Fact]
        public void CutShortSponsorship_KeepsRemainderOnTheDayItStopped()
        {
            var inv = Invoice(start: new DateTime(2026, 1, 1), days: 100, amount: 1000m);
            inv.SponsoredListing = new SponsoredListing { CampaignEndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc) };

            // Jan 1..30 served (30 days x $10) plus the $700 unserved remainder recognized Jan 31.
            var jan = SponsorAccrual.EarnedBetween(inv, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), Now);
            Assert.Equal(1000m, jan);
            Assert.Equal(0m, SponsorAccrual.EarnedBetween(inv, new DateTime(2026, 2, 1), new DateTime(2026, 3, 1), Now));
        }

        [Fact]
        public void PulledAdvertiser_StopsEarningToday()
        {
            var inv = Invoice(start: new DateTime(2026, 10, 1), days: 100, amount: 1000m, status: DirectoryStatus.Removed);
            Assert.Equal(Now.Date, SponsorAccrual.ServedEnd(inv, Now));
        }

        // ---------------- report ----------------
        [Fact]
        public void Report_Totals_AreLargestFirst()
        {
            var oneTime = new List<RevenueItem>
            {
                new () { Source = RevenueSource.SubmissionReviews, PaidUtc = new DateTime(2026, 9, 12), AmountUsd = 50m },
                new () { Source = RevenueSource.VideoSponsorships, PaidUtc = new DateTime(2026, 9, 10), AmountUsd = 1000m },
                new () { Source = RevenueSource.AffiliateCommissions, PaidUtc = new DateTime(2026, 9, 11), AmountUsd = 200m },
            };

            var r = RevenueReportBuilder.Build(new List<SponsoredListingInvoice>(), oneTime, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), RevenueBasis.Cash, Now);

            var amounts = r.Totals.Select(t => t.AmountUsd).ToList();
            Assert.Equal(amounts.OrderByDescending(a => a).ToList(), amounts);
            Assert.Equal(RevenueSource.VideoSponsorships, r.Totals[0].Source);
        }

        [Fact]
        public void AllTime_StartsAtTheFirstPayment()
        {
            var oneTime = new List<RevenueItem>
            {
                new () { Source = RevenueSource.VideoSponsorships, PaidUtc = new DateTime(2025, 10, 5), AmountUsd = 10m },
                new () { Source = RevenueSource.VideoSponsorships, PaidUtc = new DateTime(2026, 1, 5), AmountUsd = 10m },
            };

            var months = RevenueReportBuilder.BuildMonths(new List<SponsoredListingInvoice>(), oneTime, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), RevenueBasis.Cash, Now);
            var trimmed = RevenueReportBuilder.TrimLeadingEmptyMonths(months);

            Assert.Equal(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), trimmed[0].Month);
            Assert.Equal(5, trimmed.Count); // Oct..Feb, empty months after the first payment are kept
        }

        [Fact]
        public void Report_SplitsBySource_CashVsEarned()
        {
            var invoices = new List<SponsoredListingInvoice>
            {
                Invoice(start: new DateTime(2026, 9, 1), days: 61, amount: 610m, created: new DateTime(2026, 9, 1)),
            };
            var oneTime = new List<RevenueItem>
            {
                new () { Source = RevenueSource.VideoSponsorships, PaidUtc = new DateTime(2026, 9, 10), AmountUsd = 1000m },
                new () { Source = RevenueSource.SubmissionReviews, PaidUtc = new DateTime(2026, 9, 12), AmountUsd = 50m },
                new () { Source = RevenueSource.VerificationReviews, PaidUtc = new DateTime(2026, 9, 13), MissingUsd = true },
                new () { Source = RevenueSource.AffiliateCommissions, PaidUtc = new DateTime(2026, 8, 31), AmountUsd = 99m }, // outside
            };

            var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            var cash = RevenueReportBuilder.Build(invoices, oneTime, from, to, RevenueBasis.Cash, Now);
            Assert.Equal(610m, Total(cash, RevenueSource.SiteSponsors));
            Assert.Equal(1000m, Total(cash, RevenueSource.VideoSponsorships));
            Assert.Equal(50m, Total(cash, RevenueSource.SubmissionReviews));
            Assert.Equal(0m, Total(cash, RevenueSource.AffiliateCommissions));
            Assert.Equal(1, cash.Totals.Single(t => t.Source == RevenueSource.VerificationReviews).MissingUsdCount);
            Assert.Equal(1660m, cash.GrandTotal);

            // Earned: only September's 30 of the 61 paid days.
            var earned = RevenueReportBuilder.Build(invoices, oneTime, from, to, RevenueBasis.Earned, Now);
            Assert.Equal(300m, Total(earned, RevenueSource.SiteSponsors));
            Assert.Equal(100m, earned.Totals.Sum(t => t.SharePct), 0);
        }

        [Fact]
        public void Months_CoverEachCalendarMonth()
        {
            var inv = Invoice(start: new DateTime(2026, 7, 1), days: 92, amount: 920m);
            var months = RevenueReportBuilder.BuildMonths(
                new[] { inv }, new List<RevenueItem>(), new DateTime(2026, 7, 1), new DateTime(2026, 10, 1), RevenueBasis.Earned, Now);

            Assert.Equal(3, months.Count);
            Assert.Equal(new[] { 310m, 310m, 300m }, months.Select(m => m.BySource[RevenueSource.SiteSponsors]).ToArray());
        }

        [Theory]
        [InlineData("this-month", "2026-10-01", "2026-10-17")]
        [InlineData("last-month", "2026-09-01", "2026-10-01")]
        [InlineData("ytd", "2026-01-01", "2026-10-17")]
        [InlineData("last-year", "2025-01-01", "2026-01-01")]
        [InlineData("last-12", "2025-10-01", "2026-10-01")]
        [InlineData("this-quarter", "2026-10-01", "2026-10-17")]
        [InlineData("bogus", "2026-10-01", "2026-10-17")]
        public void Periods_Resolve(string key, string from, string to)
        {
            var r = RevenuePeriods.Resolve(key, null, null, Now);
            Assert.Equal(DateTime.Parse(from), r.From);
            Assert.Equal(DateTime.Parse(to), r.To);
        }

        [Fact]
        public void Periods_CustomIsInclusiveOfEndDate()
        {
            var r = RevenuePeriods.Resolve("custom", new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), Now);
            Assert.Equal(new DateTime(2026, 3, 1), r.From);
            Assert.Equal(new DateTime(2026, 4, 1), r.To);
        }

        [Theory]
        [InlineData(50.0, "USD", null, 50.0)]
        [InlineData(0.5, "XMR", "300", 150.0)]
        [InlineData(0.5, "XMR", null, null)]
        [InlineData(10.0, "BTC", "300", null)]
        public void DonationUsd(double amount, string currency, string? rate, double? expected)
        {
            var usd = DonationUsdHelper.ToUsd((decimal)amount, currency, rate);
            Assert.Equal(expected is null ? null : (decimal?)(decimal)expected.Value, usd);
        }

        // ---------------- forecast ----------------
        [Fact]
        public void Forecast_ContractedRevenue_IsTheFloor_AndCountsDailyNotUpFront()
        {
            // One sponsor paid $3,650 for a year starting today; no history to learn renewals from.
            var asOf = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var inv = Invoice(start: asOf, days: 365, amount: 3650m, created: asOf);

            var r = SponsorshipForecastModel.Run(
                new[] { inv }, new List<RevenueItem>(), Options(asOf, 6), asOf);

            // October earns 31 x $10, not $3,650.
            Assert.Equal(310m, r.Months[0].Contracted);
            Assert.All(r.Months, m => Assert.True(m.Expected >= m.Contracted - 0.01m));
            Assert.Equal(new[] { 310m, 300m, 310m, 310m, 280m, 310m }, r.Months.Select(m => m.Contracted).ToArray());
        }

        [Fact]
        public void Forecast_RenewalRate_DrivesExpectedValue()
        {
            // A long history of 30-day subcategory campaigns that ALWAYS renew, at $300 each.
            var asOf = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var invoices = Chain(entryId: 1, first: asOf.AddDays(-600), count: 21, days: 30, amount: 300m);

            var r = SponsorshipForecastModel.Run(invoices, new List<RevenueItem>(), Options(asOf, 6), asOf);

            var sub = r.Tiers.Single(t => t.Type == SponsorshipType.SubcategorySponsor);
            Assert.True(sub.RenewalRatePct > 85m, $"renewal {sub.RenewalRatePct}");

            // ~$10/day keeps going: each month should expect close to its day count x $10.
            var nov = r.Months[1];
            Assert.InRange(nov.Expected, 250m, 310m);
        }

        [Fact]
        public void Forecast_PulledAdvertiser_NeverRenews()
        {
            var asOf = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var invoices = Chain(entryId: 1, first: asOf.AddDays(-600), count: 21, days: 30, amount: 300m);
            foreach (var i in invoices)
            {
                i.DirectoryEntry!.DirectoryStatus = DirectoryStatus.Removed;
            }

            var r = SponsorshipForecastModel.Run(invoices, new List<RevenueItem>(), Options(asOf, 6), asOf);

            // Nothing contracted beyond today, no renewals, no other income, no new arrivals (the
            // only arrival was 600 days ago, outside the 1-year window).
            Assert.All(r.Months.Skip(1), m => Assert.Equal(0m, m.Expected));
        }

        [Fact]
        public void Forecast_MainSlotsCap_LimitsNewSponsors()
        {
            // Lots of recent Main arrivals but every slot is full: no room for new ones.
            var asOf = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var invoices = new List<SponsoredListingInvoice>();
            for (int e = 1; e <= 5; e++)
            {
                invoices.Add(Invoice(start: asOf.AddDays(-10), days: 365, amount: 3650m, created: asOf.AddDays(-10), entryId: e, type: SponsorshipType.MainSponsor));
            }

            var r = SponsorshipForecastModel.Run(invoices, new List<RevenueItem>(), Options(asOf, 3, mainSlots: 5), asOf);

            // Exactly the five contracted sponsors: 5 x $10/day.
            Assert.Equal(r.Months[1].Contracted, r.Months[1].Expected);
        }

        [Fact]
        public void Forecast_IsDeterministic_ForTheSameSeed()
        {
            var asOf = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var invoices = Chain(entryId: 1, first: asOf.AddDays(-400), count: 13, days: 30, amount: 300m);
            var a = SponsorshipForecastModel.Run(invoices, new List<RevenueItem>(), Options(asOf, 6), asOf);
            var b = SponsorshipForecastModel.Run(invoices, new List<RevenueItem>(), Options(asOf, 6), asOf);
            Assert.Equal(a.Months.Select(m => m.Expected), b.Months.Select(m => m.Expected));
        }

        [Fact]
        public void Backtest_RunsWithEnoughHistory_AndScoresSteadyRevenueWell()
        {
            var now = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);
            var invoices = Chain(entryId: 1, first: now.AddDays(-900), count: 31, days: 30, amount: 300m);

            var r = SponsorshipForecastModel.Run(
                invoices,
                new List<RevenueItem>(),
                new SponsorshipForecastModel.Options { AsOfUtc = now, HorizonMonths = 3, Simulations = 300, RunBacktests = true },
                now);

            Assert.NotEmpty(r.Backtests);
            // One sponsor, always renewing: the model still allows it might leave, so it under-shoots a little.
            Assert.All(r.Backtests, bt => Assert.True(bt.MeanAbsPctError < 25m, $"MAPE {bt.MeanAbsPctError}"));
        }

        private static SponsorshipForecastModel.Options Options(DateTime asOf, int months, int mainSlots = 5) => new ()
        {
            AsOfUtc = asOf,
            HorizonMonths = months,
            Simulations = 400,
            MainSlots = mainSlots,
            RunBacktests = false,
        };

        private static List<SponsoredListingInvoice> Chain(int entryId, DateTime first, int count, int days, decimal amount)
        {
            var list = new List<SponsoredListingInvoice>();
            var entry = new DirectoryEntry { DirectoryEntryId = entryId, Name = $"e{entryId}", DirectoryEntryKey = $"e{entryId}", DirectoryStatus = DirectoryStatus.Verified };
            for (int k = 0; k < count; k++)
            {
                var start = first.AddDays(k * days);
                var inv = Invoice(start: start, days: days, amount: amount, created: start, entryId: entryId);
                inv.DirectoryEntry = entry;
                list.Add(inv);
            }

            return list;
        }

        private static SponsoredListingInvoice Invoice(
            DateTime start,
            int days,
            decimal amount,
            DateTime? created = null,
            int entryId = 1,
            SponsorshipType type = SponsorshipType.SubcategorySponsor,
            DirectoryStatus status = DirectoryStatus.Verified)
        {
            var s = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            return new SponsoredListingInvoice
            {
                DirectoryEntryId = entryId,
                DirectoryEntry = new DirectoryEntry { DirectoryEntryId = entryId, Name = $"e{entryId}", DirectoryEntryKey = $"e{entryId}", DirectoryStatus = status },
                SponsorshipType = type,
                SubCategoryId = type == SponsorshipType.SubcategorySponsor ? 7 : null,
                CampaignStartDate = s,
                CampaignEndDate = s.AddDays(days),
                Amount = amount,
                Currency = Currency.USD,
                PaymentStatus = PaymentStatus.Paid,
                CreateDate = DateTime.SpecifyKind(created ?? s, DateTimeKind.Utc),
            };
        }

        private static decimal Total(RevenueReport r, RevenueSource s) => r.Totals.Single(t => t.Source == s).AmountUsd;
    }
}
