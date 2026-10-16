using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Forecasting
{
    // Revenue forecast built from how sponsorships actually behave, not a trend line:
    //
    //   1. CONTRACTED: every campaign already paid for earns its daily rate (amount / days) until
    //      it ends. A year paid up front is ~1/365 per day for a year, not a spike.
    //   2. RENEWALS: when a live advertiser's campaign ends, it renews with the probability that
    //      campaigns in that tier have historically renewed, keeps its last duration, and pays
    //      the tier's RECENT price for that duration (prices have risen over time).
    //   3. NEW SPONSORS: arrive at the rate they have over the last year, but only while a slot
    //      in that tier is free (Main has a hard cap).
    //   4. OTHER INCOME (video, affiliate, donations): sampled from the last 12 months.
    //
    // Thousands of simulated futures give the expected value and a confidence band per month.
    // A backtest replays the model from 6 and 12 months ago (using only data known then) and
    // scores it against what actually happened.
    public static class SponsorshipForecastModel
    {
        private const int RenewalLookbackDays = 540;
        private const int ArrivalLookbackDays = 365;
        private const int RenewEarlyToleranceDays = 10;
        private const int RenewLateWindowDays = 45;
        private const int RecentPricesPerBucket = 6;
        private const double ArrivalDecayDays = 260.0; // ~180-day half-life

        private static readonly SponsorshipType[] Tiers =
        {
            SponsorshipType.MainSponsor,
            SponsorshipType.CategorySponsor,
            SponsorshipType.SubcategorySponsor,
        };

        public sealed class Options
        {
            public DateTime AsOfUtc { get; init; }

            public int HorizonMonths { get; init; } = 12;

            public int Simulations { get; init; } = 2000;

            // e.g. 0.8 => 10th–90th percentile band.
            public double Confidence { get; init; } = 0.8;

            public int Seed { get; init; } = 20261016;

            // Use advertisers' CURRENT status (pulled advertisers never renew). Backtests turn this
            // off, because whether someone gets pulled later wasn't known at the time.
            public bool UseCurrentStatus { get; init; } = true;

            public int MainSlots { get; init; } = 5;

            public int CategorySlots { get; init; } = int.MaxValue;

            public int SubcategorySlots { get; init; } = int.MaxValue;

            public bool RunBacktests { get; init; } = true;

            // Price change assumed per year for renewals and new sponsors (0.10 = +10%/yr).
            // 0 = today's prices hold.
            public double PriceGrowthPerYear { get; init; }

            // Backtests only: price each simulated sale at what was ACTUALLY charged around that
            // date (instead of prices known when the forecast was made). This isolates how well
            // renewals and new sponsors are predicted from pricing decisions made later.
            public bool UseActualPricesAtSaleTime { get; init; }
        }

        public sealed class Month
        {
            public DateTime MonthStart { get; init; }

            // Earned before the forecast starts (only the current, partial month has any).
            public decimal ActualSoFar { get; init; }

            // Earned from campaigns already paid for (certain, barring refunds).
            public decimal Contracted { get; init; }

            public decimal Expected { get; init; }

            public decimal Low { get; init; }

            public decimal High { get; init; }

            public decimal SponsorExpected { get; init; }

            public decimal OtherExpected { get; init; }

            // Money expected to arrive (renewal and new-sponsor payments are paid up front).
            public decimal CashExpected { get; init; }

            public decimal CashLow { get; init; }

            public decimal CashHigh { get; init; }
        }

        public sealed class Tier
        {
            public SponsorshipType Type { get; init; }

            public int CampaignsEnded { get; init; }

            public int CampaignsRenewed { get; init; }

            public decimal RenewalRatePct { get; init; }

            public decimal NewSponsorsPerMonth { get; init; }

            public decimal RecentDailyRate { get; init; }

            public int ActiveNow { get; init; }

            public int Capacity { get; init; }

            public decimal ContractedDailyRunRate { get; init; }

            public decimal SoldOutPctLast90Days { get; init; }
        }

        public sealed class BacktestMonth
        {
            public DateTime MonthStart { get; init; }

            public decimal Predicted { get; init; }

            public decimal Low { get; init; }

            public decimal High { get; init; }

            public decimal Actual { get; init; }

            // Predicted when each sale is priced at what was actually charged then.
            public decimal PredictedAtActualPrices { get; init; }
        }

        public sealed class Backtest
        {
            public DateTime AsOfUtc { get; init; }

            public List<BacktestMonth> Months { get; init; } = new ();

            // Mean absolute percentage error of the expected value.
            public decimal MeanAbsPctError { get; init; }

            // Share of months whose actual fell inside the band.
            public decimal CoveragePct { get; init; }

            // Total predicted vs actual over the whole window.
            public decimal TotalPredicted { get; init; }

            public decimal TotalActual { get; init; }

            // The same replay, but pricing each sale at what was actually charged then: how well
            // renewals and new sponsors were predicted, ignoring later price changes.
            public decimal VolumeOnlyMeanAbsPctError { get; init; }

            public decimal VolumeOnlyTotalPredicted { get; init; }
        }

        public sealed class Result
        {
            public DateTime AsOfUtc { get; init; }

            public double Confidence { get; init; }

            public List<Month> Months { get; init; } = new ();

            public List<Tier> Tiers { get; init; } = new ();

            public decimal OtherMonthlyAverage { get; init; }

            public decimal ContractedRemaining { get; init; }

            public decimal ExpectedTotal => this.Months.Sum(m => m.Expected);

            public List<Backtest> Backtests { get; init; } = new ();
        }

        public static Result Run(
            IReadOnlyList<SponsoredListingInvoice> paidInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            Options o,
            DateTime realNowUtc)
        {
            var asOf = o.AsOfUtc.Date;
            var known = paidInvoices.Where(i => i.CreateDate < asOf.AddDays(1)).ToList();
            var otherKnown = oneTime.Where(i => i.PaidUtc < asOf).ToList();

            var chains = BuildChains(known, asOf, o.UseCurrentStatus, realNowUtc);
            var priceSource = o.UseActualPricesAtSaleTime ? paidInvoices.ToList() : known;
            var tierModels = Tiers.ToDictionary(t => t, t => BuildTierModel(t, chains, known, priceSource, asOf, o));

            // ---- horizon: whole calendar months starting with the current one ----
            var firstMonth = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthStarts = Enumerable.Range(0, o.HorizonMonths).Select(firstMonth.AddMonths).ToList();
            var horizonEnd = firstMonth.AddMonths(o.HorizonMonths);
            int days = (int)(horizonEnd - asOf).TotalDays;
            var dayMonth = new int[days];
            for (int d = 0; d < days; d++)
            {
                var date = asOf.AddDays(d);
                dayMonth[d] = ((date.Year - firstMonth.Year) * 12) + date.Month - firstMonth.Month;
            }

            // ---- contracted (deterministic) ----
            var contractedDaily = new double[days];
            foreach (var c in chains)
            {
                foreach (var inv in c.Invoices)
                {
                    var start = inv.CampaignStartDate.Date;
                    var end = o.UseCurrentStatus ? SponsorAccrual.ServedEnd(inv, realNowUtc) : inv.CampaignEndDate.Date;
                    var rate = (double)SponsorAccrual.DailyRate(inv);
                    var s = Math.Max(0, (int)(start - asOf).TotalDays);
                    var e = Math.Min(days, (int)(end - asOf).TotalDays);
                    for (int d = s; d < e; d++)
                    {
                        contractedDaily[d] += rate;
                    }
                }
            }

            // ---- other income history: last 12 complete months ----
            var otherHistory = new List<double>();
            for (int k = 12; k >= 1; k--)
            {
                var mFrom = firstMonth.AddMonths(-k);
                var mTo = mFrom.AddMonths(1);
                otherHistory.Add((double)otherKnown.Where(i => i.PaidUtc >= mFrom && i.PaidUtc < mTo).Sum(i => i.AmountUsd));
            }

            // Drop leading months from before other income existed at all.
            int firstOther = otherHistory.FindIndex(v => v > 0);
            if (firstOther > 0)
            {
                otherHistory = otherHistory.Skip(firstOther).ToList();
            }

            if (otherHistory.Count == 0 || firstOther < 0)
            {
                otherHistory = new List<double> { 0d };
            }

            // ---- already earned this (partial) month before asOf ----
            var actualSoFar = (double)known.Sum(i => SponsorAccrual.EarnedBetween(i, firstMonth, asOf, asOf.AddDays(-1)))
                              + (double)otherKnown.Where(i => i.PaidUtc >= firstMonth && i.PaidUtc < asOf).Sum(i => i.AmountUsd);

            // ---- simulate ----
            int months = o.HorizonMonths;
            var sims = Math.Max(50, o.Simulations);
            var earnedSims = new double[months][];
            var cashSims = new double[months][];
            var sponsorSims = new double[months][];
            var otherSims = new double[months][];
            for (int m = 0; m < months; m++)
            {
                earnedSims[m] = new double[sims];
                cashSims[m] = new double[sims];
                sponsorSims[m] = new double[sims];
                otherSims[m] = new double[sims];
            }

            var contractedByMonth = new double[months];
            for (int d = 0; d < days; d++)
            {
                contractedByMonth[dayMonth[d]] += contractedDaily[d];
            }

            var rng = new Random(o.Seed);
            var simEarned = new double[days];
            var simCash = new double[days];
            for (int s = 0; s < sims; s++)
            {
                Array.Clear(simEarned);
                Array.Clear(simCash);
                SimulateOnce(chains, tierModels, o, days, simEarned, simCash, rng);

                var sponsorMonth = new double[months];
                var cashMonth = new double[months];
                for (int d = 0; d < days; d++)
                {
                    sponsorMonth[dayMonth[d]] += simEarned[d];
                    cashMonth[dayMonth[d]] += simCash[d];
                }

                for (int m = 0; m < months; m++)
                {
                    // Other income: a whole month for future months, the remaining fraction for the
                    // current one.
                    var other = otherHistory[rng.Next(otherHistory.Count)];
                    if (m == 0)
                    {
                        var monthDays = DateTime.DaysInMonth(firstMonth.Year, firstMonth.Month);
                        other *= (double)(monthDays - (asOf - firstMonth).TotalDays) / monthDays;
                    }

                    var sponsor = contractedByMonth[m] + sponsorMonth[m];
                    var extra = m == 0 ? actualSoFar : 0d;
                    sponsorSims[m][s] = sponsor;
                    otherSims[m][s] = other;
                    earnedSims[m][s] = sponsor + other + extra;
                    cashSims[m][s] = cashMonth[m] + other;
                }
            }

            double lowQ = (1.0 - o.Confidence) / 2.0;
            double highQ = 1.0 - lowQ;
            var monthsOut = new List<Month>(months);
            for (int m = 0; m < months; m++)
            {
                Array.Sort(earnedSims[m]);
                Array.Sort(cashSims[m]);
                monthsOut.Add(new Month
                {
                    MonthStart = monthStarts[m],
                    ActualSoFar = m == 0 ? Money(actualSoFar) : 0m,
                    Contracted = Money(contractedByMonth[m] + (m == 0 ? actualSoFar : 0d)),
                    Expected = Money(earnedSims[m].Average()),
                    Low = Money(Quantile(earnedSims[m], lowQ)),
                    High = Money(Quantile(earnedSims[m], highQ)),
                    SponsorExpected = Money(sponsorSims[m].Average()),
                    OtherExpected = Money(otherSims[m].Average()),
                    CashExpected = Money(cashSims[m].Average()),
                    CashLow = Money(Quantile(cashSims[m], lowQ)),
                    CashHigh = Money(Quantile(cashSims[m], highQ)),
                });
            }

            var tiers = Tiers.Select(t =>
            {
                var tm = tierModels[t];
                return new Tier
                {
                    Type = t,
                    CampaignsEnded = tm.Ended,
                    CampaignsRenewed = tm.Renewed,
                    RenewalRatePct = Math.Round((decimal)(tm.RenewProbability * 100), 1),
                    NewSponsorsPerMonth = Math.Round((decimal)(tm.ArrivalsPerDay * 30.4375), 2),
                    RecentDailyRate = Money(tm.RecentDailyRate),
                    ActiveNow = chains.Count(c => c.Tier == t && c.ActiveEndDay(asOf) > 0),
                    Capacity = Capacity(t, o),
                    SoldOutPctLast90Days = Math.Round((decimal)tm.FullPctLast90, 0),
                    ContractedDailyRunRate = Money(chains.Where(c => c.Tier == t)
                        .SelectMany(c => c.Invoices)
                        .Where(i => i.CampaignStartDate.Date <= asOf && i.CampaignEndDate.Date > asOf)
                        .Sum(i => (double)SponsorAccrual.DailyRate(i))),
                };
            }).ToList();

            var backtests = new List<Backtest>();
            if (o.RunBacktests)
            {
                foreach (var monthsBack in new[] { 12, 6 })
                {
                    var bt = RunBacktest(paidInvoices, oneTime, o, realNowUtc, monthsBack);
                    if (bt != null)
                    {
                        backtests.Add(bt);
                    }
                }
            }

            return new Result
            {
                AsOfUtc = asOf,
                Confidence = o.Confidence,
                Months = monthsOut,
                Tiers = tiers,
                OtherMonthlyAverage = Money(otherHistory.Average()),
                ContractedRemaining = Money(contractedDaily.Sum()),
                Backtests = backtests,
            };
        }

        // Replays the model as of the first of the month `monthsBack` ago and compares each full
        // month it predicted with what was actually earned.
        private static Backtest? RunBacktest(
            IReadOnlyList<SponsoredListingInvoice> paidInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            Options o,
            DateTime realNowUtc,
            int monthsBack)
        {
            var currentMonth = new DateTime(realNowUtc.Year, realNowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var asOf = currentMonth.AddMonths(-monthsBack);
            var horizon = Math.Min(monthsBack, 6);

            // Need at least a year of history before the backtest point to learn from.
            if (!paidInvoices.Any(i => i.CreateDate < asOf.AddMonths(-12)))
            {
                return null;
            }

            Options Replay(bool actualPrices) => new ()
            {
                AsOfUtc = asOf,
                HorizonMonths = horizon,
                Simulations = Math.Min(o.Simulations, 600),
                Confidence = o.Confidence,
                Seed = o.Seed + monthsBack,
                UseCurrentStatus = false,
                MainSlots = o.MainSlots,
                CategorySlots = o.CategorySlots,
                SubcategorySlots = o.SubcategorySlots,
                RunBacktests = false,
                UseActualPricesAtSaleTime = actualPrices,
            };

            var r = Run(paidInvoices, oneTime, Replay(false), realNowUtc);
            var volume = Run(paidInvoices, oneTime, Replay(true), realNowUtc);

            var actualMonths = RevenueReportBuilder.BuildMonths(
                paidInvoices, oneTime, asOf, asOf.AddMonths(horizon), RevenueBasis.Earned, realNowUtc);

            var rows = r.Months.Zip(actualMonths, (p, a) => new BacktestMonth
            {
                MonthStart = p.MonthStart,
                Predicted = p.Expected,
                Low = p.Low,
                High = p.High,
                Actual = Math.Round(a.Total, 2),
            }).Zip(volume.Months, (row, v) => new BacktestMonth
            {
                MonthStart = row.MonthStart,
                Predicted = row.Predicted,
                Low = row.Low,
                High = row.High,
                Actual = row.Actual,
                PredictedAtActualPrices = v.Expected,
            }).ToList();

            var scored = rows.Where(x => x.Actual > 0m).ToList();
            var volumeScored = volume.Months.Zip(actualMonths, (p, a) => (Predicted: p.Expected, Actual: a.Total))
                .Where(x => x.Actual > 0m)
                .ToList();
            return new Backtest
            {
                VolumeOnlyMeanAbsPctError = volumeScored.Count == 0 ? 0m
                    : Math.Round(volumeScored.Average(x => Math.Abs(x.Predicted - x.Actual) / x.Actual) * 100m, 1),
                VolumeOnlyTotalPredicted = volume.Months.Sum(m => m.Expected),
                AsOfUtc = asOf,
                Months = rows,
                MeanAbsPctError = scored.Count == 0 ? 0m
                    : Math.Round(scored.Average(x => Math.Abs(x.Predicted - x.Actual) / x.Actual) * 100m, 1),
                CoveragePct = rows.Count == 0 ? 0m
                    : Math.Round(100m * rows.Count(x => x.Actual >= x.Low && x.Actual <= x.High) / rows.Count, 0),
                TotalPredicted = rows.Sum(x => x.Predicted),
                TotalActual = rows.Sum(x => x.Actual),
            };
        }

        private static void SimulateOnce(
            List<Chain> chains,
            Dictionary<SponsorshipType, TierModel> tiers,
            Options o,
            int days,
            double[] earned,
            double[] cash,
            Random rng)
        {
            var asOf = o.AsOfUtc.Date;
            var occupancy = Tiers.ToDictionary(t => t, t => 0);

            // Parameter uncertainty: each simulated future draws its own renewal rate (Beta over the
            // observed renewals) and demand level (Gamma around the observed sales), so the band also
            // reflects how little history some estimates rest on, not just luck.
            var renewP = Tiers.ToDictionary(t => t, t => SampleBeta(rng, tiers[t].Renewed + 1.0, tiers[t].Ended - tiers[t].Renewed + 1.0));
            var demand = Tiers.ToDictionary(t => t, t => SampleGamma(rng, tiers[t].SalesObserved + 1.0) / (tiers[t].SalesObserved + 1.0));
            var active = new List<SimSlot>();

            foreach (var c in chains)
            {
                var endDay = c.ActiveEndDay(asOf);
                var tm = tiers[c.Tier];
                if (endDay > 0)
                {
                    active.Add(new SimSlot(c.Tier, endDay, c.LastDays, c.Live));
                    occupancy[c.Tier]++;
                }
                else if (c.Live && endDay > -30 && rng.NextDouble() < tm.LateRenewProbability)
                {
                    // Lapsed in the last month: some advertisers renew a little late.
                    var price = tm.PriceFor(c.LastDays, c.LastDailyRate, 0);
                    AddCampaign(earned, cash, 0, c.LastDays, price, days);
                    active.Add(new SimSlot(c.Tier, c.LastDays, c.LastDays, true));
                    occupancy[c.Tier]++;
                }
            }

            for (int d = 0; d < days; d++)
            {
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var slot = active[i];
                    if (slot.EndDay != d)
                    {
                        continue;
                    }

                    var tm = tiers[slot.Tier];
                    if (slot.Live && rng.NextDouble() < renewP[slot.Tier])
                    {
                        var price = tm.PriceFor(slot.Days, tm.RecentDailyRate, d);
                        AddCampaign(earned, cash, d, slot.Days, price, days);
                        slot.EndDay = d + slot.Days;
                    }
                    else
                    {
                        active.RemoveAt(i);
                        occupancy[slot.Tier]--;
                    }
                }

                foreach (var t in Tiers)
                {
                    var tm = tiers[t];
                    var cap = Capacity(t, o);
                    var lambda = tm.ArrivalsPerFreeSlotDay > 0 && cap != int.MaxValue
                        ? tm.ArrivalsPerFreeSlotDay * Math.Max(0, cap - occupancy[t])
                        : tm.ArrivalsPerDay;
                    lambda *= demand[t];
                    var arrivals = Poisson(rng, Math.Min(lambda, 5.0));
                    for (int a = 0; a < arrivals; a++)
                    {
                        if (occupancy[t] >= Capacity(t, o) || tm.NewDurations.Count == 0)
                        {
                            break;
                        }

                        var len = tm.NewDurations[rng.Next(tm.NewDurations.Count)];
                        var price = tm.PriceFor(len, tm.RecentDailyRate, d);
                        AddCampaign(earned, cash, d, len, price, days);
                        active.Add(new SimSlot(t, d + len, len, true));
                        occupancy[t]++;
                    }
                }
            }
        }

        private static void AddCampaign(double[] earned, double[] cash, int startDay, int length, double price, int days)
        {
            if (length <= 0 || price <= 0)
            {
                return;
            }

            cash[startDay] += price;
            var rate = price / length;
            var end = Math.Min(days, startDay + length);
            for (int d = startDay; d < end; d++)
            {
                earned[d] += rate;
            }
        }

        private static int Capacity(SponsorshipType t, Options o) => t switch
        {
            SponsorshipType.MainSponsor => o.MainSlots,
            SponsorshipType.CategorySponsor => o.CategorySlots,
            _ => o.SubcategorySlots,
        };

        private static List<Chain> BuildChains(
            List<SponsoredListingInvoice> known, DateTime asOf, bool useCurrentStatus, DateTime realNowUtc)
        {
            return known
                .Where(i => Tiers.Contains(i.SponsorshipType))
                .GroupBy(i => (i.DirectoryEntryId, i.SponsorshipType, Scope: ScopeId(i)))
                .Select(g =>
                {
                    var ordered = g.OrderBy(i => i.CampaignStartDate).ThenBy(i => i.CreateDate).ToList();
                    var last = ordered.OrderByDescending(i => i.CampaignEndDate).First();
                    var status = last.DirectoryEntry?.DirectoryStatus;
                    var live = !useCurrentStatus || status is null || SponsorshipIncomeAnalyzer.IsLiveAdvertiser(status.Value);
                    var end = useCurrentStatus ? SponsorAccrual.ServedEnd(last, realNowUtc) : last.CampaignEndDate.Date;
                    return new Chain
                    {
                        Tier = g.Key.SponsorshipType,
                        Invoices = ordered,
                        End = end,
                        LastDays = SponsorAccrual.PaidDays(last),
                        LastDailyRate = (double)SponsorAccrual.DailyRate(last),
                        Live = live,
                    };
                })
                .ToList();
        }

        private static int ScopeId(SponsoredListingInvoice i) => i.SponsorshipType switch
        {
            SponsorshipType.CategorySponsor => i.CategoryId ?? 0,
            SponsorshipType.SubcategorySponsor => i.SubCategoryId ?? 0,
            _ => 0,
        };

        private static TierModel BuildTierModel(
            SponsorshipType tier,
            List<Chain> chains,
            List<SponsoredListingInvoice> known,
            List<SponsoredListingInvoice> priceSource,
            DateTime asOf,
            Options o)
        {
            var tierChains = chains.Where(c => c.Tier == tier).ToList();

            // Renewal: of the campaigns that ended between ~18 and 1.5 months ago, how many were
            // followed by another campaign in the same slot.
            int ended = 0, renewed = 0, renewedLate = 0;
            foreach (var c in tierChains)
            {
                var inv = c.Invoices;
                for (int k = 0; k < inv.Count; k++)
                {
                    var end = inv[k].CampaignEndDate.Date;
                    if (end < asOf.AddDays(-RenewalLookbackDays) || end > asOf.AddDays(-RenewLateWindowDays))
                    {
                        continue;
                    }

                    ended++;
                    var next = inv.Skip(k + 1).FirstOrDefault(n =>
                        n.CampaignStartDate.Date >= end.AddDays(-RenewEarlyToleranceDays) &&
                        n.CampaignStartDate.Date <= end.AddDays(RenewLateWindowDays));
                    if (next != null)
                    {
                        renewed++;
                        if (next.CampaignStartDate.Date > end.AddDays(3))
                        {
                            renewedLate++;
                        }
                    }
                }
            }

            // New sponsors: slots whose first-ever campaign was bought in the last year.
            // New sales: every campaign that is NOT a renewal of the one before it in the same slot,
            // i.e. a first-ever purchase OR an advertiser coming back after a gap. (Counting only
            // first-ever purchases misses returning sponsors, which are neither renewals nor new.)
            var firsts = tierChains.SelectMany(c => c.Invoices.Where((inv, k) => k == 0 || !IsRenewalOf(inv, c.Invoices[k - 1]))).ToList();
            var recentFirsts = firsts.Where(f => f.CreateDate >= asOf.AddDays(-ArrivalLookbackDays)).ToList();

            // Recent arrivals count more (half-life ~6 months), so a rising arrival rate isn't
            // diluted by a slow start to the year: rate = sum(weights) / integral of the weight.
            double weighted = recentFirsts.Sum(f => Math.Exp(-(asOf - f.CreateDate).TotalDays / ArrivalDecayDays));
            double weightIntegral = ArrivalDecayDays * (1 - Math.Exp(-ArrivalLookbackDays / ArrivalDecayDays));

            // Demand per FREE slot-day. A tier that is always full (Main) only ever shows a sale when
            // a slot opens, so measuring sales over all days badly understates how fast an open slot
            // is taken. Dividing by the (recency-weighted) free slot-days fixes that censoring.
            var capacity = Capacity(tier, o);
            double perFreeSlotDay = 0;
            int fullDays90 = 0;
            if (capacity != int.MaxValue)
            {
                double freeSlotDays = 0;
                for (int back = 1; back <= ArrivalLookbackDays; back++)
                {
                    var day = asOf.AddDays(-back);
                    int occupied = tierChains.Count(c => c.Invoices.Any(i => i.CampaignStartDate.Date <= day && i.CampaignEndDate.Date > day));
                    freeSlotDays += Math.Max(0, capacity - occupied) * Math.Exp(-back / ArrivalDecayDays);
                    if (back <= 90 && occupied >= capacity)
                    {
                        fullDays90++;
                    }
                }

                // Always full with sales anyway: treat it as ~2 free slot-days per sale (fills fast).
                perFreeSlotDay = weighted <= 0 ? 0 : weighted / Math.Max(freeSlotDays, weighted * 2);
            }
            var durationPool = firsts.Where(f => f.CreateDate >= asOf.AddDays(-2 * ArrivalLookbackDays)).ToList();
            if (durationPool.Count == 0)
            {
                durationPool = firsts;
            }

            var tierInvoices = known.Where(i => i.SponsorshipType == tier).OrderByDescending(i => i.CreateDate).ToList();
            var recentRates = tierInvoices.Take(RecentPricesPerBucket).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();

            return new TierModel
            {
                Ended = ended,
                Renewed = renewed,

                // Laplace-smoothed so a thin history doesn't give 0% or 100%.
                RenewProbability = (renewed + 1.0) / (ended + 2.0),
                LateRenewProbability = (renewedLate + 1.0) / (ended + 2.0),
                ArrivalsPerDay = weighted / weightIntegral,
                ArrivalsPerFreeSlotDay = perFreeSlotDay,
                SalesObserved = recentFirsts.Count,
                FullPctLast90 = capacity == int.MaxValue ? 0 : fullDays90 / 90.0 * 100.0,
                NewDurations = durationPool.Select(SponsorAccrual.PaidDays).ToList(),
                RecentDailyRate = recentRates.Count == 0 ? 0d : Median(recentRates),
                RecentInvoices = tierInvoices.Take(60).ToList(),
                PriceHistory = priceSource.Where(i => i.SponsorshipType == tier).OrderByDescending(i => i.CreateDate).ToList(),
                AsOf = asOf,
                PriceGrowthPerYear = o.PriceGrowthPerYear,
                UseActualPricesAtSaleTime = o.UseActualPricesAtSaleTime,
            };
        }

        // A campaign renews the previous one in its slot when it starts around when that one ended.
        private static bool IsRenewalOf(SponsoredListingInvoice next, SponsoredListingInvoice previous)
        {
            var end = previous.CampaignEndDate.Date;
            var start = next.CampaignStartDate.Date;
            return start >= end.AddDays(-RenewEarlyToleranceDays) && start <= end.AddDays(RenewLateWindowDays);
        }

        // Gamma(shape, 1) via Marsaglia–Tsang.
        private static double SampleGamma(Random rng, double shape)
        {
            if (shape < 1)
            {
                return SampleGamma(rng, shape + 1) * Math.Pow(rng.NextDouble(), 1.0 / shape);
            }

            double d = shape - (1.0 / 3.0), c = 1.0 / Math.Sqrt(9 * d);
            while (true)
            {
                double x, v;
                do
                {
                    // Standard normal via Box–Muller.
                    x = Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
                    v = 1 + (c * x);
                }
                while (v <= 0);

                v = v * v * v;
                var u = rng.NextDouble();
                if (u < 1 - (0.0331 * x * x * x * x) || Math.Log(u) < (0.5 * x * x) + (d * (1 - v + Math.Log(v))))
                {
                    return d * v;
                }
            }
        }

        private static double SampleBeta(Random rng, double a, double b)
        {
            var x = SampleGamma(rng, a);
            var y = SampleGamma(rng, b);
            return x / (x + y);
        }

        private static int Poisson(Random rng, double lambda)
        {
            if (lambda <= 0)
            {
                return 0;
            }

            double l = Math.Exp(-lambda), p = 1.0;
            int k = 0;
            do
            {
                k++;
                p *= rng.NextDouble();
            }
            while (p > l);
            return k - 1;
        }

        private static double Quantile(double[] sorted, double q)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }

            var pos = q * (sorted.Length - 1);
            var lo = (int)Math.Floor(pos);
            var hi = (int)Math.Ceiling(pos);
            return sorted[lo] + ((sorted[hi] - sorted[lo]) * (pos - lo));
        }

        private static double Median(List<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[(s.Count / 2) - 1] + s[s.Count / 2]) / 2.0;
        }

        private static decimal Money(double v) => Math.Round((decimal)v, 2);

        private sealed class Chain
        {
            public SponsorshipType Tier { get; init; }

            public List<SponsoredListingInvoice> Invoices { get; init; } = new ();

            // When the slot's paid (and served) time runs out.
            public DateTime End { get; init; }

            public int LastDays { get; init; }

            public double LastDailyRate { get; init; }

            public bool Live { get; init; }

            public int ActiveEndDay(DateTime asOf) => (int)(this.End - asOf).TotalDays;
        }

        private sealed class TierModel
        {
            public int Ended { get; init; }

            public int Renewed { get; init; }

            public double RenewProbability { get; init; }

            public double LateRenewProbability { get; init; }

            public double ArrivalsPerDay { get; init; }

            // 0 when the tier has no meaningful cap (then ArrivalsPerDay applies as-is).
            public double ArrivalsPerFreeSlotDay { get; init; }

            // New sales in the arrival window (drives how uncertain the demand estimate is).
            public int SalesObserved { get; init; }

            // Share of the last 90 days every slot in the tier was taken.
            public double FullPctLast90 { get; init; }

            public List<int> NewDurations { get; init; } = new ();

            public double RecentDailyRate { get; init; }

            public List<SponsoredListingInvoice> RecentInvoices { get; init; } = new ();

            // Newest first. Normally the same as RecentInvoices' source; for "actual price"
            // backtests it also holds invoices sold after the forecast date.
            public List<SponsoredListingInvoice> PriceHistory { get; init; } = new ();

            public DateTime AsOf { get; init; }

            public double PriceGrowthPerYear { get; init; }

            public bool UseActualPricesAtSaleTime { get; init; }

            // Price of a campaign of `length` days sold `saleDay` days into the forecast: the median
            // daily rate of the latest invoices of a similar length (longer campaigns are cheaper
            // per day), falling back to recent invoices of any length, then to the advertiser's own
            // last rate; then grown by the assumed yearly price change.
            public double PriceFor(int length, double fallbackDailyRate, int saleDay)
            {
                var pool = this.RecentInvoices;
                if (this.UseActualPricesAtSaleTime)
                {
                    var saleDate = this.AsOf.AddDays(saleDay + 1);
                    pool = this.PriceHistory.Where(i => i.CreateDate < saleDate).Take(60).ToList();
                }

                var similar = pool
                    .Where(i => Bucket(SponsorAccrual.PaidDays(i)) == Bucket(length))
                    .Take(RecentPricesPerBucket)
                    .Select(i => (double)SponsorAccrual.DailyRate(i))
                    .ToList();
                var anyLength = pool.Take(RecentPricesPerBucket).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();

                var rate = similar.Count > 0 ? Median(similar)
                    : anyLength.Count > 0 ? Median(anyLength)
                    : fallbackDailyRate;

                if (this.PriceGrowthPerYear != 0 && !this.UseActualPricesAtSaleTime)
                {
                    rate *= Math.Pow(1 + this.PriceGrowthPerYear, saleDay / 365.0);
                }

                return rate * length;
            }

            private static int Bucket(int days) => days switch
            {
                <= 14 => 0,
                <= 45 => 1,
                <= 120 => 2,
                <= 240 => 3,
                _ => 4,
            };
        }

        private sealed class SimSlot
        {
            public SimSlot(SponsorshipType tier, int endDay, int days, bool live)
            {
                this.Tier = tier;
                this.EndDay = endDay;
                this.Days = Math.Max(1, days);
                this.Live = live;
            }

            public SponsorshipType Tier { get; }

            public int EndDay { get; set; }

            public int Days { get; }

            public bool Live { get; }
        }
    }
}
