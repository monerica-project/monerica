using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Revenue;
using Xunit.Abstractions;

namespace DirectoryManager.Web.Tests.Revenue
{
    // Manual probe: runs the forecast on an exported CSV snapshot when REVDATA points at its
    // folder. Skipped (does nothing) otherwise, so it never runs in normal test passes.
    public class RealDataForecastProbe
    {
        private readonly ITestOutputHelper output;

        public RealDataForecastProbe(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void Probe()
        {
            var dir = Environment.GetEnvironmentVariable("REVDATA");
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            var ci = CultureInfo.InvariantCulture;
            var invoices = File.ReadAllLines(Path.Combine(dir, "invoices.csv")).Where(l => l.Length > 0).Select(l =>
            {
                var f = l.Split(',');
                var entry = new DirectoryEntry { DirectoryEntryId = int.Parse(f[1], ci), Name = f[1], DirectoryEntryKey = f[1], DirectoryStatus = (DirectoryStatus)int.Parse(f[2], ci) };
                var inv = new SponsoredListingInvoice
                {
                    SponsoredListingInvoiceId = int.Parse(f[0], ci),
                    DirectoryEntryId = entry.DirectoryEntryId,
                    DirectoryEntry = entry,
                    SponsorshipType = (SponsorshipType)int.Parse(f[3], ci),
                    CategoryId = int.Parse(f[4], ci),
                    SubCategoryId = int.Parse(f[5], ci),
                    CampaignStartDate = Utc(f[6]),
                    CampaignEndDate = Utc(f[7]),
                    CreateDate = Utc(f[8]),
                    Amount = decimal.Parse(f[9], ci),
                    Currency = (Currency)int.Parse(f[10], ci),
                    PaidInCurrency = (Currency)int.Parse(f[11], ci),
                    PaidAmount = decimal.Parse(f[12], ci),
                    PaymentStatus = PaymentStatus.Paid,
                };
                if (f[13].Length > 0)
                {
                    inv.SponsoredListing = new SponsoredListing { CampaignEndDate = Utc(f[13]) };
                }

                return inv;
            }).ToList();

            var oneTime = File.ReadAllLines(Path.Combine(dir, "onetime.csv")).Where(l => l.Length > 0).Select(l =>
            {
                var f = l.Split(',');
                return new RevenueItem { Source = (RevenueSource)int.Parse(f[0], ci), PaidUtc = Utc(f[1]), AmountUsd = decimal.Parse(f[2], ci) };
            }).ToList();

            var now = DateTime.UtcNow;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = SponsorshipForecastModel.Run(invoices, oneTime, new SponsorshipForecastModel.Options
            {
                AsOfUtc = now,
                HorizonMonths = 12,
                MainSlots = 5,
                CategorySlots = 20,
                SubcategorySlots = 212,
            }, now);
            this.output.WriteLine($"ran in {sw.ElapsedMilliseconds} ms");

            var hist = RevenueReportBuilder.BuildMonths(invoices, oneTime, new DateTime(now.Year, now.Month, 1).AddMonths(-12), new DateTime(now.Year, now.Month, 1), RevenueBasis.Earned, now);
            var cash = RevenueReportBuilder.BuildMonths(invoices, oneTime, new DateTime(now.Year, now.Month, 1).AddMonths(-12), new DateTime(now.Year, now.Month, 1), RevenueBasis.Cash, now);
            for (int i = 0; i < hist.Count; i++)
            {
                this.output.WriteLine($"HIST {hist[i].Month:yyyy-MM} earned {hist[i].Total,10:0} sponsors {hist[i].BySource[RevenueSource.SiteSponsors],10:0}   cash {cash[i].Total,10:0}");
            }

            foreach (var m in r.Months)
            {
                this.output.WriteLine($"FCST {m.MonthStart:yyyy-MM} so-far {m.ActualSoFar,8:0} contracted {m.Contracted,8:0} expected {m.Expected,8:0} [{m.Low,8:0} - {m.High,8:0}] sponsors {m.SponsorExpected,8:0} other {m.OtherExpected,6:0} cash {m.CashExpected,8:0}");
            }

            foreach (var t in r.Tiers)
            {
                this.output.WriteLine($"TIER {t.Type} active {t.ActiveNow}/{t.Capacity} renew {t.RenewalRatePct}% ({t.CampaignsRenewed}/{t.CampaignsEnded}) new/mo {t.NewSponsorsPerMonth} recent/day {t.RecentDailyRate} earning/day {t.ContractedDailyRunRate}");
            }

            var outDir = Environment.GetEnvironmentVariable("REVDATA_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                var report = RevenueReportBuilder.Build(invoices, oneTime, new DateTime(now.Year, 1, 1), now.Date.AddDays(1), RevenueBasis.Earned, now);
                File.WriteAllBytes(Path.Combine(outDir, "pie.png"), Charting.RevenuePlotting.Pie(report));
                File.WriteAllBytes(Path.Combine(outDir, "monthly.png"), Charting.RevenuePlotting.MonthlyStacked(hist, RevenueBasis.Earned));
                var hist18 = RevenueReportBuilder.BuildMonths(invoices, oneTime, new DateTime(now.Year, now.Month, 1).AddMonths(-18), new DateTime(now.Year, now.Month, 1), RevenueBasis.Earned, now);
                File.WriteAllBytes(Path.Combine(outDir, "forecast.png"), Charting.RevenuePlotting.Forecast(hist18, r));
                if (r.Backtests.Count > 0)
                {
                    File.WriteAllBytes(Path.Combine(outDir, "backtest.png"), Charting.RevenuePlotting.Backtest(r.Backtests[^1], r.Confidence));
                }
            }

            foreach (var bt in r.Backtests)
            {
                this.output.WriteLine($"BACKTEST {bt.AsOfUtc:yyyy-MM-dd} MAPE {bt.MeanAbsPctError}% coverage {bt.CoveragePct}% predicted {bt.TotalPredicted:0} actual {bt.TotalActual:0} | actual-prices MAPE {bt.VolumeOnlyMeanAbsPctError}% predicted {bt.VolumeOnlyTotalPredicted:0}");
                foreach (var m in bt.Months)
                {
                    this.output.WriteLine($"   {m.MonthStart:yyyy-MM} predicted {m.Predicted,8:0} [{m.Low,8:0}-{m.High,8:0}] actual {m.Actual,8:0}");
                }
            }
        }

        private static DateTime Utc(string s) => DateTime.SpecifyKind(DateTime.Parse(s, CultureInfo.InvariantCulture), DateTimeKind.Utc);
    }
}
