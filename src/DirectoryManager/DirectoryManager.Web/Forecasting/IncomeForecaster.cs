using System;
using System.Collections.Generic;
using System.Linq;

namespace DirectoryManager.Web.Forecasting
{
    /// <summary>
    /// Pure, deterministic income forecaster.
    ///
    /// Model: fits an ordinary-least-squares linear trend to monthly booked revenue
    /// (revenue ~ a + b*t), then projects it forward. Uncertainty is expressed as a
    /// band of +/- z * residual-standard-deviation around the expected line, where z
    /// is derived from the requested confidence level. The current (partial) month is
    /// prorated by the fraction of days remaining so the forecast starts "from now".
    ///
    /// It is intentionally free of EF/HTTP concerns so it can be unit tested and reused
    /// by both the report view and the chart endpoint.
    /// </summary>
    public static class IncomeForecaster
    {
        public sealed class MonthPoint
        {
            public DateTime Month { get; set; }
            public decimal Expected { get; set; }
            public decimal Low { get; set; }
            public decimal High { get; set; }
            public decimal CumExpected { get; set; }
            public decimal CumLow { get; set; }
            public decimal CumHigh { get; set; }
        }

        public sealed class Milestone
        {
            public decimal Target { get; set; }
            public DateTime? ExpectedDate { get; set; }
            public DateTime? EarliestDate { get; set; }
            public DateTime? LatestDate { get; set; }
        }

        public sealed class Result
        {
            public bool HasEnoughData { get; set; }
            public int HistoryMonthsUsed { get; set; }
            public decimal RecentMonthlyAverage { get; set; }
            public decimal TrendSlopePerMonth { get; set; }
            public double ConfidenceZ { get; set; }

            public decimal ProjectedHorizonTotalExpected { get; set; }
            public decimal ProjectedHorizonTotalLow { get; set; }
            public decimal ProjectedHorizonTotalHigh { get; set; }

            public List<MonthPoint> Months { get; set; } = new ();
            public List<Milestone> Milestones { get; set; } = new ();
        }

        /// <summary>
        /// Build a forecast.
        /// </summary>
        /// <param name="historyMonths">Chronological, contiguous first-of-month dates (completed months only).</param>
        /// <param name="historyValues">Booked revenue per history month, same order as <paramref name="historyMonths"/>.</param>
        /// <param name="nowUtc">Current moment (UTC). The forecast starts at this month, prorated.</param>
        /// <param name="horizonMonths">How many months to project (current month counts as the first).</param>
        /// <param name="confidenceZ">Z multiplier for the band (e.g. 1.282 for ~80%).</param>
        public static Result Build(
            IReadOnlyList<DateTime> historyMonths,
            IReadOnlyList<decimal> historyValues,
            DateTime nowUtc,
            int horizonMonths,
            double confidenceZ)
        {
            if (horizonMonths < 1)
            {
                horizonMonths = 1;
            }

            var result = new Result
            {
                ConfidenceZ = confidenceZ,
                HistoryMonthsUsed = historyValues?.Count ?? 0
            };

            int n = historyValues?.Count ?? 0;
            if (n == 0)
            {
                result.HasEnoughData = false;
                return result;
            }

            // --- fit trend: revenue ~ a + b*t, t = 0..n-1 ---
            double a;
            double b;
            double residualStd;

            if (n >= 3)
            {
                double sx = 0, sy = 0, sxx = 0, sxy = 0;
                for (int t = 0; t < n; t++)
                {
                    double x = t;
                    double y = (double)historyValues[t];
                    sx += x;
                    sy += y;
                    sxx += x * x;
                    sxy += x * y;
                }

                double denom = (n * sxx) - (sx * sx);
                b = denom != 0 ? ((n * sxy) - (sx * sy)) / denom : 0;
                a = (sy - (b * sx)) / n;

                double sse = 0;
                for (int t = 0; t < n; t++)
                {
                    double pred = a + (b * t);
                    double e = (double)historyValues[t] - pred;
                    sse += e * e;
                }

                residualStd = Math.Sqrt(sse / Math.Max(1, n - 2));
            }
            else
            {
                // Too few points for a meaningful slope: flat projection at the mean.
                a = (double)historyValues.Average();
                b = 0;

                if (n >= 2)
                {
                    double mean = a;
                    double ss = 0;
                    foreach (var v in historyValues)
                    {
                        double e = (double)v - mean;
                        ss += e * e;
                    }

                    residualStd = Math.Sqrt(ss / (n - 1));
                }
                else
                {
                    // Single data point: assume a 25% spread so the band isn't degenerate.
                    residualStd = Math.Abs((double)historyValues[0]) * 0.25;
                }
            }

            result.TrendSlopePerMonth = (decimal)b;
            result.RecentMonthlyAverage = historyValues
                .Skip(Math.Max(0, n - 3))
                .DefaultIfEmpty(0m)
                .Average();
            result.HasEnoughData = true;

            // --- project forward ---
            var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            int daysInCurrent = DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month);
            int daysRemaining = Math.Max(0, daysInCurrent - (nowUtc.Day - 1)); // include today
            double currentRemainingFraction = daysInCurrent > 0 ? (double)daysRemaining / daysInCurrent : 1.0;

            decimal cumE = 0m, cumL = 0m, cumH = 0m;
            for (int k = 0; k < horizonMonths; k++)
            {
                var month = currentMonthStart.AddMonths(k);
                int t = n + k; // continue the trend index past the history
                double full = Math.Max(0, a + (b * t));
                double frac = k == 0 ? currentRemainingFraction : 1.0;

                double expected = full * frac;
                double spread = residualStd * confidenceZ * frac;
                double low = Math.Max(0, expected - spread);
                double high = Math.Max(0, expected + spread);

                cumE += (decimal)expected;
                cumL += (decimal)low;
                cumH += (decimal)high;

                result.Months.Add(new MonthPoint
                {
                    Month = month,
                    Expected = (decimal)expected,
                    Low = (decimal)low,
                    High = (decimal)high,
                    CumExpected = cumE,
                    CumLow = cumL,
                    CumHigh = cumH
                });
            }

            result.ProjectedHorizonTotalExpected = cumE;
            result.ProjectedHorizonTotalLow = cumL;
            result.ProjectedHorizonTotalHigh = cumH;

            result.Milestones = BuildMilestones(result.Months, nowUtc, cumE);
            return result;
        }

        private static List<Milestone> BuildMilestones(
            IReadOnlyList<MonthPoint> months,
            DateTime nowUtc,
            decimal horizonTotalExpected)
        {
            var milestones = new List<Milestone>();
            if (horizonTotalExpected <= 0m || months.Count == 0)
            {
                return milestones;
            }

            decimal step = NiceStep(horizonTotalExpected / 8m);
            if (step <= 0m)
            {
                return milestones;
            }

            for (decimal target = step; target <= horizonTotalExpected + (step / 2m); target += step)
            {
                var clamped = Math.Min(target, horizonTotalExpected);
                milestones.Add(new Milestone
                {
                    Target = clamped,
                    ExpectedDate = FindCrossDate(months, nowUtc, p => p.CumExpected, clamped),

                    // High band accumulates fastest -> you reach a target soonest in the optimistic case.
                    EarliestDate = FindCrossDate(months, nowUtc, p => p.CumHigh, clamped),

                    // Low band accumulates slowest -> latest you'd reach it in the conservative case.
                    LatestDate = FindCrossDate(months, nowUtc, p => p.CumLow, clamped)
                });
            }

            return milestones;
        }

        private static DateTime? FindCrossDate(
            IReadOnlyList<MonthPoint> months,
            DateTime nowUtc,
            Func<MonthPoint, decimal> cumulativeSelector,
            decimal target)
        {
            decimal prev = 0m;
            for (int k = 0; k < months.Count; k++)
            {
                var p = months[k];
                decimal cur = cumulativeSelector(p);

                if (cur >= target)
                {
                    decimal segSpan = cur - prev;
                    double f = segSpan > 0m ? (double)((target - prev) / segSpan) : 0.0;
                    f = Math.Clamp(f, 0.0, 1.0);

                    DateTime segStart;
                    DateTime segEndOpen = p.Month.AddMonths(1);

                    if (k == 0)
                    {
                        // Current partial month accrues from "now" to month end.
                        segStart = nowUtc;
                    }
                    else
                    {
                        segStart = p.Month;
                    }

                    var totalTicks = (segEndOpen - segStart).Ticks;
                    var offset = TimeSpan.FromTicks((long)(totalTicks * f));
                    return segStart + offset;
                }

                prev = cur;
            }

            return null; // not reached within the horizon
        }

        /// <summary>
        /// Round a raw step up to a "nice" 1 / 2 / 5 * 10^k value so milestone targets read cleanly.
        /// </summary>
        private static decimal NiceStep(decimal raw)
        {
            if (raw <= 0m)
            {
                return 0m;
            }

            double x = (double)raw;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(x)));
            double n = x / magnitude;

            double nice =
                n < 1.5 ? 1 :
                n < 3 ? 2 :
                n < 7 ? 5 : 10;

            return (decimal)(nice * magnitude);
        }
    }
}
