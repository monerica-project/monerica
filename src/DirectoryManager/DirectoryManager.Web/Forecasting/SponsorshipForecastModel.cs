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
        private const int RenewEarlyToleranceDays = 10;
        private const int RenewLateWindowDays = 45;
        private const int RecentPricesPerBucket = 6;

        // Main is one product with one going price that moves with demand, so the latest sale of a
        // similar length is the best guide to the next (a median of several lags a moving price:
        // measured on replays, near-term error 20% with the last six, 14% with the latest).
        private const int MainRecentPrices = 1;

        // A recent Main price trend carries on for a while but fades: half of the trend is carried
        // forward and each month keeps 70% of the month before (at most about 13% in total). On
        // replays this trimmed the error a little and halved how often the result fell outside the range.
        private const double MomentumScale = 0.5;
        private const double MomentumKeptPerMonth = 0.7;

        // How much of a price swing is still there a month later (the rest has faded back).
        private const double PriceLevelKeptPerMonth = 0.8;
        private const double ArrivalDecayDays = 260.0; // ~180-day half-life

        // Extra spread on site-sponsor income per month ahead (in logs), so the stated range holds
        // about as often as it says on replays.
        private const double ModelErrorBase = 0.10;
        private const double ModelErrorPerMonth = 0.05;

        // The spread was sized on replays that look six months ahead, so it stops growing there.
        private const int ModelErrorGrowsForMonths = 5;

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

            /// <summary>
            /// How far back the model learns from: new-sponsor arrivals over this many days, renewal
            /// rates over 1.5x this, and every other stream's months over this period. Default 1 year.
            /// </summary>
            public int LookbackDays { get; init; } = 365;

            public int RenewalLookbackDays => (int)Math.Round(this.LookbackDays * 1.5);

            public int LookbackMonths => Math.Max(1, (int)Math.Round(this.LookbackDays / 30.4375));

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

            /// <summary>Site sponsors' range for the month (same confidence as the total).</summary>
            public decimal SponsorLow { get; init; }

            /// <summary>Site-sponsor income already earned this month (current month only).</summary>
            public decimal SponsorSoFar { get; init; }

            public decimal SponsorHigh { get; init; }

            /// <summary>Each other income stream on its own (expected and range).</summary>
            public List<SourceForecast> Sources { get; init; } = new ();

            // Money expected to arrive (renewal and new-sponsor payments are paid up front).
            public decimal CashExpected { get; init; }

            public decimal CashLow { get; init; }

            public decimal CashHigh { get; init; }
        }

        /// <summary>How much history a stream has, and how much of its recent pace the forecast trusts.</summary>
        public sealed class SourceStatus
        {
            public RevenueSource Source { get; init; }

            public DateTime FirstPaidUtc { get; init; }

            public double MonthsOfData { get; init; }

            /// <summary>0 = too new to forecast, 1 = full trust; in between for a few months of data.</summary>
            public double Weight { get; init; }

            public decimal ReceivedSoFar { get; init; }
        }

        /// <summary>One income stream's forecast for one month.</summary>
        public sealed class SourceForecast
        {
            public RevenueSource Source { get; init; }

            /// <summary>Already received this month (current month only).</summary>
            public decimal ActualSoFar { get; init; }

            public decimal Expected { get; init; }

            public decimal Low { get; init; }

            public decimal High { get; init; }
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

            /// <summary>History and trust level of each non-sponsor stream.</summary>
            public Dictionary<RevenueSource, SourceStatus> SourceStatuses { get; init; } = new ();

            /// <summary>Monthly average of each other income stream (months since it started, last 12).</summary>
            public Dictionary<RevenueSource, decimal> SourceMonthlyAverage { get; init; } = new ();

            public decimal ContractedRemaining { get; init; }

            public decimal ExpectedTotal => this.Months.Sum(m => m.Expected);

            public List<Backtest> Backtests { get; init; } = new ();

            /// <summary>How each stream's forecast did when replayed from each of the last 12 month starts.</summary>
            public List<StreamScore> Scorecard { get; init; } = new ();
        }

        /// <summary>
        /// One stream's track record on replays: the forecast was re-made from each of the last 12
        /// month starts with only what was known then, and each month it predicted is compared
        /// with what came in.
        /// </summary>
        public sealed class StreamScore
        {
            /// <summary>The stream, or null for all revenue together.</summary>
            public RevenueSource? Source { get; init; }

            /// <summary>True for months 4 to 6 ahead, false for months 1 to 3.</summary>
            public bool Far { get; init; }

            public int MonthsScored { get; init; }

            public decimal AverageActual { get; init; }

            /// <summary>Half of the months missed by less than this many dollars.</summary>
            public decimal TypicalMissUsd { get; init; }

            /// <summary>Typical miss as a share of the stream's average month.</summary>
            public decimal TypicalMissPct { get; init; }

            /// <summary>Predicted total against actual total: negative means it predicted too little.</summary>
            public decimal BiasPct { get; init; }

            public decimal InsideRangePct { get; init; }
        }

        /// <summary>Under this many months of data a stream isn't projected at all.</summary>
        public const double MinMonthsToForecast = 2;

        /// <summary>From this many months of data a stream's recent pace is trusted fully.</summary>
        public const double FullTrustMonths = 6;

        /// <summary>How much of a stream's recent pace to project, from how long it has existed.</summary>
        /// <returns>0 to 1.</returns>
        public static double TrustWeight(double monthsOfData) =>
            monthsOfData < MinMonthsToForecast ? 0d : Math.Min(1d, monthsOfData / FullTrustMonths);

        /// <summary>A stream's numbers for one forecast month (site sponsors include what's already earned this month).</summary>
        /// <returns>Expected, low and high.</returns>
        public static (decimal Expected, decimal Low, decimal High) StreamMonth(Month m, RevenueSource s)
        {
            if (s == RevenueSource.SiteSponsors)
            {
                return (m.SponsorExpected + m.SponsorSoFar, m.SponsorLow + m.SponsorSoFar, m.SponsorHigh + m.SponsorSoFar);
            }

            var f = m.Sources.FirstOrDefault(x => x.Source == s);
            return f == null ? (0m, 0m, 0m) : (f.Expected, f.Low, f.High);
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

            // ---- other income history, per stream: the look-back period's complete months ----
            // Each stream (video sponsorships, affiliates, the review payments) is forecast on its
            // own from its own months, starting from its first paid month, then added up.
            var otherSources = otherKnown.Select(i => i.Source).Distinct().OrderBy(x => x).ToList();
            var sourceHistory = new Dictionary<RevenueSource, List<double>>();
            foreach (var src in otherSources)
            {
                var h = new List<double>();
                for (int k = o.LookbackMonths; k >= 1; k--)
                {
                    var mFrom = firstMonth.AddMonths(-k);
                    var mTo = mFrom.AddMonths(1);
                    h.Add((double)otherKnown.Where(i => i.Source == src && i.PaidUtc >= mFrom && i.PaidUtc < mTo).Sum(i => i.AmountUsd));
                }

                // Drop leading months from before this stream existed. Its very first month is
                // usually a partial one (it started mid-month), so that goes too once there are at
                // least two full months after it.
                int first = h.FindIndex(v => v > 0);
                var own = first < 0 ? new List<double> { 0d } : h.Skip(first).ToList();
                var firstPaidAt = otherKnown.Where(i => i.Source == src).Select(i => (DateTime?)i.PaidUtc).Min();
                bool startedInsideWindow = first >= 0 && firstPaidAt >= firstMonth.AddMonths(-o.LookbackMonths) && firstPaidAt.Value.Day > 5;
                sourceHistory[src] = startedInsideWindow && own.Count >= 3 ? own.Skip(1).ToList() : own;
            }

            // A stream that only started recently isn't projected from a few weeks of sales: under
            // MinMonthsToForecast months it isn't projected at all, then trust grows to full at FullTrustMonths.
            var sourceStatus = otherSources.ToDictionary(src => src, src =>
            {
                var firstPaid = otherKnown.Where(i => i.Source == src).Min(i => i.PaidUtc);
                var monthsOfData = Math.Max(0d, (asOf - firstPaid).TotalDays / 30.4375);
                return new SourceStatus
                {
                    Source = src,
                    FirstPaidUtc = firstPaid,
                    MonthsOfData = Math.Round(monthsOfData, 1),
                    Weight = TrustWeight(monthsOfData),
                    ReceivedSoFar = Money((double)otherKnown.Where(i => i.Source == src).Sum(i => i.AmountUsd)),
                };
            });

            // ---- already earned this (partial) month before asOf ----
            var actualSoFar = (double)known.Sum(i => SponsorAccrual.EarnedBetween(i, firstMonth, asOf, asOf.AddDays(-1)))
                              + (double)otherKnown.Where(i => i.PaidUtc >= firstMonth && i.PaidUtc < asOf).Sum(i => i.AmountUsd);
            var sponsorSoFar = (double)known.Sum(i => SponsorAccrual.EarnedBetween(i, firstMonth, asOf, asOf.AddDays(-1)));
            var sourceSoFar = otherSources.ToDictionary(
                src => src,
                src => (double)otherKnown.Where(i => i.Source == src && i.PaidUtc >= firstMonth && i.PaidUtc < asOf).Sum(i => i.AmountUsd));

            // ---- simulate ----
            int months = o.HorizonMonths;
            var sims = Math.Max(50, o.Simulations);
            var earnedSims = new double[months][];
            var cashSims = new double[months][];
            var sponsorSims = new double[months][];
            var otherSims = new double[months][];
            var sourceSims = otherSources.ToDictionary(src => src, _ => new double[months][]);
            for (int m = 0; m < months; m++)
            {
                foreach (var src in otherSources)
                {
                    sourceSims[src][m] = new double[sims];
                }

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

            var sponsorMean = new double[months];
            var earnedMean = new double[months];
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
                var modelShock = SampleNormal(rng);
                for (int d = 0; d < days; d++)
                {
                    sponsorMonth[dayMonth[d]] += simEarned[d];
                    cashMonth[dayMonth[d]] += simCash[d];
                }

                for (int m = 0; m < months; m++)
                {
                    // Other income: a whole month for future months, the remaining fraction for the
                    // current one.
                    // Each stream draws one of its own past months independently.
                    double remainingShare = 1d;
                    if (m == 0)
                    {
                        var monthDays = DateTime.DaysInMonth(firstMonth.Year, firstMonth.Month);
                        remainingShare = (double)(monthDays - (asOf - firstMonth).TotalDays) / monthDays;
                    }

                    double other = 0d;
                    foreach (var src in otherSources)
                    {
                        var h = sourceHistory[src];
                        var v = h[rng.Next(h.Count)] * remainingShare * sourceStatus[src].Weight;
                        sourceSims[src][m][s] = v + (m == 0 ? sourceSoFar[src] : 0d);
                        other += v;
                    }

                    // Things the simulation doesn't model (a sponsor pulled early, an unusual deal,
                    // a price change) move a month by more than luck in renewals and sales explains.
                    // One shared draw per simulated future, growing with distance, sized on replays.
                    var modelSigma = ModelErrorBase + (ModelErrorPerMonth * Math.Min(m, ModelErrorGrowsForMonths));
                    var sponsorPlain = contractedByMonth[m] + sponsorMonth[m];
                    var sponsor = sponsorPlain * Math.Exp(modelSigma * modelShock);

                    // The shock only widens the range: "expected" is the average without it.
                    sponsorMean[m] += sponsorPlain / sims;
                    var extra = m == 0 ? actualSoFar : 0d;
                    sponsorSims[m][s] = sponsor;
                    otherSims[m][s] = other;
                    earnedSims[m][s] = sponsor + other + extra;
                    earnedMean[m] += (sponsorPlain + other + extra) / sims;
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
                Array.Sort(sponsorSims[m]);
                int mm = m;
                var sources = otherSources.Select(src =>
                {
                    var arr = sourceSims[src][mm];
                    Array.Sort(arr);
                    return new SourceForecast
                    {
                        Source = src,
                        ActualSoFar = mm == 0 ? Money(sourceSoFar[src]) : 0m,
                        Expected = Money(arr.Average()),
                        Low = Money(Quantile(arr, lowQ)),
                        High = Money(Quantile(arr, highQ)),
                    };
                }).ToList();
                monthsOut.Add(new Month
                {
                    MonthStart = monthStarts[m],
                    ActualSoFar = m == 0 ? Money(actualSoFar) : 0m,
                    Contracted = Money(contractedByMonth[m] + (m == 0 ? actualSoFar : 0d)),
                    Expected = Money(earnedMean[m]),
                    Low = Money(Quantile(earnedSims[m], lowQ)),
                    High = Money(Quantile(earnedSims[m], highQ)),
                    SponsorExpected = Money(sponsorMean[m]),
                    OtherExpected = Money(otherSims[m].Average()),
                    SponsorLow = Money(Quantile(sponsorSims[m], lowQ)),
                    SponsorSoFar = m == 0 ? Money(sponsorSoFar) : 0m,
                    SponsorHigh = Money(Quantile(sponsorSims[m], highQ)),
                    Sources = sources,
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
                Scorecard = o.RunBacktests ? BuildScorecard(paidInvoices, oneTime, o, realNowUtc) : new List<StreamScore>(),
                AsOfUtc = asOf,
                Confidence = o.Confidence,
                Months = monthsOut,
                Tiers = tiers,
                OtherMonthlyAverage = Money(sourceHistory.Values.Sum(h => h.Average())),
                SourceMonthlyAverage = sourceHistory.ToDictionary(kv => kv.Key, kv => Money(kv.Value.Average())),
                SourceStatuses = sourceStatus,
                ContractedRemaining = Money(contractedDaily.Sum()),
                Backtests = backtests,
            };
        }

        // Replays the forecast from each of the last 12 month starts and scores every stream on the
        // months it predicted, split into the near months (1 to 3 ahead) and the far ones (4 to 6).
        private static List<StreamScore> BuildScorecard(
            IReadOnlyList<SponsoredListingInvoice> paidInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            Options o,
            DateTime realNowUtc)
        {
            var currentMonth = new DateTime(realNowUtc.Year, realNowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var samples = new Dictionary<(int Source, bool Far), List<(decimal Pred, decimal Low, decimal High, decimal Act)>>();
            void Add(int source, bool far, decimal pred, decimal low, decimal high, decimal act)
            {
                if (!samples.TryGetValue((source, far), out var list))
                {
                    samples[(source, far)] = list = new ();
                }

                list.Add((pred, low, high, act));
            }

            for (int back = 12; back >= 1; back--)
            {
                var asOf = currentMonth.AddMonths(-back);
                if (!paidInvoices.Any(i => i.CreateDate < asOf.AddMonths(-9)))
                {
                    continue;
                }

                var horizon = Math.Min(back, 6);
                var replay = Run(paidInvoices, oneTime, new Options
                {
                    AsOfUtc = asOf,
                    HorizonMonths = horizon,
                    Simulations = Math.Min(o.Simulations, 300),
                    Confidence = o.Confidence,
                    Seed = o.Seed + 100 + back,
                    UseCurrentStatus = false,
                    MainSlots = o.MainSlots,
                    CategorySlots = o.CategorySlots,
                    SubcategorySlots = o.SubcategorySlots,
                    RunBacktests = false,
                    LookbackDays = o.LookbackDays,
                }, realNowUtc);
                var actual = RevenueReportBuilder.BuildMonths(paidInvoices, oneTime, asOf, asOf.AddMonths(horizon), RevenueBasis.Earned, realNowUtc);
                for (int k = 0; k < horizon && k < actual.Count; k++)
                {
                    var m = replay.Months[k];
                    bool far = k >= 3;
                    Add(0, far, m.Expected, m.Low, m.High, actual[k].Total);
                    foreach (var src in RevenueReportBuilder.Sources)
                    {
                        var (exp, low, high) = StreamMonth(m, src);
                        var act = actual[k].BySource.TryGetValue(src, out var a) ? a : 0m;

                        // A stream that had not started when the forecast was made is not a test:
                        // nothing could have predicted it.
                        if (src != RevenueSource.SiteSponsors && !oneTime.Any(i => i.Source == src && i.PaidUtc < asOf))
                        {
                            continue;
                        }

                        Add((int)src, far, exp, low, high, act);
                    }
                }
            }

            return samples
                .Where(kv => kv.Value.Count >= 3)
                .OrderBy(kv => kv.Key.Source).ThenBy(kv => kv.Key.Far)
                .Select(kv =>
                {
                    var xs = kv.Value;
                    var misses = xs.Select(x => (double)Math.Abs(x.Pred - x.Act)).ToList();
                    var avgActual = xs.Average(x => x.Act);
                    var typical = (decimal)Median(misses);
                    var totalActual = xs.Sum(x => x.Act);
                    return new StreamScore
                    {
                        Source = kv.Key.Source == 0 ? null : (RevenueSource)kv.Key.Source,
                        Far = kv.Key.Far,
                        MonthsScored = xs.Count,
                        AverageActual = Math.Round(avgActual, 2),
                        TypicalMissUsd = Math.Round(typical, 2),
                        TypicalMissPct = avgActual <= 0m ? 0m : Math.Round(typical / avgActual * 100m, 1),
                        BiasPct = totalActual <= 0m ? 0m : Math.Round((xs.Sum(x => x.Pred) - totalActual) / totalActual * 100m, 1),
                        InsideRangePct = Math.Round(100m * xs.Count(x => x.Act >= x.Low && x.Act <= x.High) / xs.Count, 0),
                    };
                })
                .ToList();
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
            // Renewal depends a lot on tenure (a first campaign renews about half as often as one
            // that has already renewed twice), so each tenure group draws its own rate, shrunk
            // toward the tier's overall rate where the group has little history.
            var renewP = Tiers.ToDictionary(t => t, t =>
            {
                var tm = tiers[t];
                var p = new double[3];
                for (int g = 0; g < 3; g++)
                {
                    const double prior = 4.0;
                    p[g] = SampleBeta(
                        rng,
                        tm.RenewedByStreak[g] + (prior * tm.RenewProbability) + 0.5,
                        tm.EndedByStreak[g] - tm.RenewedByStreak[g] + (prior * (1 - tm.RenewProbability)) + 0.5);
                }

                return p;
            });

            // Price level: each simulated future lets the tier's going price drift (a random walk
            // with the month-to-month volatility its prices have shown), so the range reflects that
            // prices move, which is the largest source of error on replays.
            var priceLevel = Tiers.ToDictionary(t => t, t =>
            {
                // The going price wanders but is pulled back toward where it is now (it rises
                // when slots are scarce and falls when one sits open), so the spread levels off
                // instead of growing without limit. The level averages 1: volatility widens the
                // range, it does not lift the expected price.
                var level = new double[days + 1];
                double noise = 0, variance = 0, trend = 0;
                var sigmaDay = tiers[t].PriceVolatilityPerMonth / Math.Sqrt(30.4375);
                var keep = Math.Pow(PriceLevelKeptPerMonth, 1 / 30.4375);
                var momentum = o.UseActualPricesAtSaleTime ? 0d : tiers[t].PriceMomentumPerMonth / 30.4375;
                for (int d = 0; d <= days; d++)
                {
                    level[d] = Math.Exp(trend + noise - (variance / 2));
                    noise = (keep * noise) + (sigmaDay * SampleNormal(rng));
                    variance = (keep * keep * variance) + (sigmaDay * sigmaDay);
                    trend += momentum * Math.Pow(MomentumKeptPerMonth, d / 30.4375);
                }

                return level;
            });
            var demand = Tiers.ToDictionary(t => t, t => SampleGamma(rng, tiers[t].SalesObserved + 1.0) / (tiers[t].SalesObserved + 1.0));
            var active = new List<SimSlot>();

            foreach (var c in chains)
            {
                var endDay = c.ActiveEndDay(asOf);
                var tm = tiers[c.Tier];
                if (endDay > 0)
                {
                    active.Add(new SimSlot(c.Tier, endDay, c.LastDays, c.Live, c.LastDailyRate, c.RenewalStreak));
                    occupancy[c.Tier]++;
                }
                else if (c.Live && endDay > -30 && rng.NextDouble() < tm.LateRenewProbability)
                {
                    // Lapsed in the last month: some advertisers renew a little late.
                    var price = tm.PriceFor(c.LastDays, c.LastDailyRate, 0, rng, renewal: true) * priceLevel[c.Tier][0];
                    AddCampaign(earned, cash, 0, c.LastDays, price, days);
                    active.Add(new SimSlot(c.Tier, c.LastDays, c.LastDays, true, c.LastDailyRate, c.RenewalStreak + 1));
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
                    if (slot.Live && rng.NextDouble() < renewP[slot.Tier][Math.Min(slot.Streak, 2)])
                    {
                        var price = tm.PriceFor(slot.Days, slot.OwnDailyRate, d, rng, renewal: true) * priceLevel[slot.Tier][d];
                        AddCampaign(earned, cash, d, slot.Days, price, days);
                        slot.EndDay = d + slot.Days;
                        slot.Streak++;
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
                        var price = tm.PriceFor(len, tm.RecentDailyRate, d, rng, renewal: false) * priceLevel[t][d];
                        AddCampaign(earned, cash, d, len, price, days);
                        active.Add(new SimSlot(t, d + len, len, true, price / len / priceLevel[t][d], 0));
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
                        RenewalStreak = StreakAt(ordered, ordered.Count - 1),
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
            var endedByStreak = new int[3];
            var renewedByStreak = new int[3];
            foreach (var c in tierChains)
            {
                var inv = c.Invoices;
                for (int k = 0; k < inv.Count; k++)
                {
                    var end = inv[k].CampaignEndDate.Date;
                    if (end < asOf.AddDays(-o.RenewalLookbackDays) || end > asOf.AddDays(-RenewLateWindowDays))
                    {
                        continue;
                    }

                    ended++;
                    var group = Math.Min(StreakAt(inv, k), 2);
                    endedByStreak[group]++;
                    var next = inv.Skip(k + 1).FirstOrDefault(n =>
                        n.CampaignStartDate.Date >= end.AddDays(-RenewEarlyToleranceDays) &&
                        n.CampaignStartDate.Date <= end.AddDays(RenewLateWindowDays));
                    if (next != null)
                    {
                        renewed++;
                        renewedByStreak[group]++;
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
            var recentFirsts = firsts.Where(f => f.CreateDate >= asOf.AddDays(-o.LookbackDays)).ToList();

            // Recent arrivals count more (half-life ~6 months), so a rising arrival rate isn't
            // diluted by a slow start to the year: rate = sum(weights) / integral of the weight.
            double weighted = recentFirsts.Sum(f => Math.Exp(-(asOf - f.CreateDate).TotalDays / ArrivalDecayDays));
            double weightIntegral = ArrivalDecayDays * (1 - Math.Exp(-o.LookbackDays / ArrivalDecayDays));

            // Demand per FREE slot-day. A tier that is always full (Main) only ever shows a sale when
            // a slot opens, so measuring sales over all days badly understates how fast an open slot
            // is taken. Dividing by the (recency-weighted) free slot-days fixes that censoring.
            var capacity = Capacity(tier, o);
            double perFreeSlotDay = 0;
            int fullDays90 = 0;
            if (capacity != int.MaxValue)
            {
                double freeSlotDays = 0;
                for (int back = 1; back <= o.LookbackDays; back++)
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
            var durationPool = firsts.Where(f => f.CreateDate >= asOf.AddDays(-2 * o.LookbackDays)).ToList();
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
                EndedByStreak = endedByStreak,
                RenewedByStreak = renewedByStreak,
                Tier = tier,
                PriceVolatilityPerMonth = PriceVolatility(tierInvoices, asOf),
                PriceMomentumPerMonth = tier == SponsorshipType.MainSponsor ? MomentumScale * PriceMomentum(tierInvoices, asOf) : 0d,

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

        // Renewals in a row leading up to campaign k of a slot.
        private static int StreakAt(List<SponsoredListingInvoice> inv, int k)
        {
            int streak = 0;
            for (int j = k; j > 0 && IsRenewalOf(inv[j], inv[j - 1]); j--)
            {
                streak++;
            }

            return streak;
        }

        // Month-to-month volatility of a tier's going price: the spread of the changes between
        // consecutive monthly median daily rates over the last 18 months, per month of gap.
        private static double PriceVolatility(List<SponsoredListingInvoice> tierInvoicesNewestFirst, DateTime asOf)
        {
            var monthly = tierInvoicesNewestFirst
                .Where(i => i.CreateDate >= asOf.AddMonths(-18))
                .GroupBy(i => (i.CreateDate.Year * 12) + i.CreateDate.Month)
                .OrderBy(g => g.Key)
                .Select(g => (Month: g.Key, Rate: Median(g.Select(i => (double)SponsorAccrual.DailyRate(i)).ToList())))
                .Where(x => x.Rate > 0)
                .ToList();
            var changes = new List<double>();
            for (int k = 1; k < monthly.Count; k++)
            {
                changes.Add(Math.Log(monthly[k].Rate / monthly[k - 1].Rate) / Math.Sqrt(monthly[k].Month - monthly[k - 1].Month));
            }

            if (changes.Count < 3)
            {
                return 0.10;
            }

            var rms = Math.Sqrt(changes.Average(c => c * c));
            return Math.Clamp(rms, 0.03, 0.20);
        }

        // Recent price trend per month (in logs): the last 60 days' median daily rate against the
        // 90 days before that. Zero when either window has fewer than two sales.
        private static double PriceMomentum(List<SponsoredListingInvoice> tierInvoicesNewestFirst, DateTime asOf)
        {
            var recent = tierInvoicesNewestFirst.Where(i => i.CreateDate >= asOf.AddDays(-60)).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();
            var before = tierInvoicesNewestFirst.Where(i => i.CreateDate < asOf.AddDays(-60) && i.CreateDate >= asOf.AddDays(-150)).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();
            if (recent.Count < 2 || before.Count < 2)
            {
                return 0d;
            }

            var perMonth = Math.Log(Median(recent) / Median(before)) / 2.5;
            return Math.Clamp(perMonth, -0.10, 0.10);
        }

        private static double SampleNormal(Random rng) =>
            Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());

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

            // How many times in a row the slot has been renewed up to its latest campaign.
            public int RenewalStreak { get; init; }

            public bool Live { get; init; }

            public int ActiveEndDay(DateTime asOf) => (int)(this.End - asOf).TotalDays;
        }

        private sealed class TierModel
        {
            public int Ended { get; init; }

            public int Renewed { get; init; }

            // Ended and renewed campaigns by tenure: no renewal yet, one, two or more.
            public int[] EndedByStreak { get; init; } = new int[3];

            public int[] RenewedByStreak { get; init; } = new int[3];

            public SponsorshipType Tier { get; init; }

            // Standard deviation of the month-to-month change (in logs) of the tier's going price.
            public double PriceVolatilityPerMonth { get; init; }

            // Recent trend of the going price, per month (in logs).
            public double PriceMomentumPerMonth { get; init; }

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
            public double PriceFor(int length, double ownDailyRate, int saleDay, Random rng, bool renewal)
            {
                var pool = this.RecentInvoices;
                if (this.UseActualPricesAtSaleTime)
                {
                    var saleDate = this.AsOf.AddDays(saleDay + 1);
                    pool = this.PriceHistory.Where(i => i.CreateDate < saleDate).Take(60).ToList();
                }

                double rate;
                if (this.Tier == SponsorshipType.MainSponsor)
                {
                    // One product, one going price: the latest sale of a similar length.
                    var similar = pool
                        .Where(i => Bucket(SponsorAccrual.PaidDays(i)) == Bucket(length))
                        .Take(MainRecentPrices)
                        .Select(i => (double)SponsorAccrual.DailyRate(i))
                        .ToList();
                    var anyLength = pool.Take(MainRecentPrices).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();
                    rate = similar.Count > 0 ? Median(similar)
                        : anyLength.Count > 0 ? Median(anyLength)
                        : ownDailyRate;
                }
                else if (renewal && ownDailyRate > 0)
                {
                    // Category and subcategory prices depend on the page, so a renewal pays what
                    // that advertiser paid last time.
                    rate = ownDailyRate;
                }
                else
                {
                    // A new sale lands on some page: draw one of the recent sales of a similar length.
                    var similar = pool
                        .Where(i => Bucket(SponsorAccrual.PaidDays(i)) == Bucket(length))
                        .Take(RecentPricesPerBucket)
                        .Select(i => (double)SponsorAccrual.DailyRate(i))
                        .ToList();
                    if (similar.Count == 0)
                    {
                        similar = pool.Take(RecentPricesPerBucket).Select(i => (double)SponsorAccrual.DailyRate(i)).ToList();
                    }

                    rate = similar.Count > 0 ? similar[rng.Next(similar.Count)] : ownDailyRate;
                }

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
            public SimSlot(SponsorshipType tier, int endDay, int days, bool live, double ownDailyRate, int streak)
            {
                this.Tier = tier;
                this.EndDay = endDay;
                this.Days = Math.Max(1, days);
                this.Live = live;
                this.OwnDailyRate = ownDailyRate;
                this.Streak = streak;
            }

            // What this advertiser last paid per day (category and subcategory prices differ by page).
            public double OwnDailyRate { get; }

            // Renewals in a row so far.
            public int Streak { get; set; }

            public SponsorshipType Tier { get; }

            public int EndDay { get; set; }

            public int Days { get; }

            public bool Live { get; }
        }
    }
}
