using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Revenue
{
    public static class RevenueReportBuilder
    {
        // Display order everywhere (table, legend, chart colors).
        public static readonly IReadOnlyList<RevenueSource> Sources = new[]
        {
            RevenueSource.SiteSponsors,
            RevenueSource.VideoSponsorships,
            RevenueSource.AffiliateCommissions,
            RevenueSource.SubmissionReviews,
            RevenueSource.VerificationReviews,
            RevenueSource.ProblemReportReviews,
        };

        public static string Label(RevenueSource s) => s switch
        {
            RevenueSource.SiteSponsors => "Site sponsors",
            RevenueSource.VideoSponsorships => "Video sponsorships",
            RevenueSource.AffiliateCommissions => "Affiliate commissions",
            RevenueSource.SubmissionReviews => "Submission review payments",
            RevenueSource.VerificationReviews => "Verification payments",
            RevenueSource.ProblemReportReviews => "Problem report payments",
            _ => s.ToString(),
        };

        // Revenue in [fromUtc, toUtc). sponsorInvoices must be the PAID (Paid/OverPayment) invoices.
        public static RevenueReport Build(
            IReadOnlyList<SponsoredListingInvoice> sponsorInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            DateTime fromUtc,
            DateTime toUtc,
            RevenueBasis basis,
            DateTime nowUtc,
            int decimals = 2)
        {
            var from = fromUtc.Date;
            var to = toUtc.Date;

            var amounts = Sources.ToDictionary(s => s, _ => 0m);
            var counts = Sources.ToDictionary(s => s, _ => 0);
            var missing = Sources.ToDictionary(s => s, _ => 0);

            foreach (var inv in sponsorInvoices)
            {
                var v = SponsorAmount(inv, from, to, basis, nowUtc);
                if (v > 0m)
                {
                    amounts[RevenueSource.SiteSponsors] += v;
                    counts[RevenueSource.SiteSponsors]++;
                }
            }

            foreach (var item in oneTime)
            {
                if (item.PaidUtc < from || item.PaidUtc >= to)
                {
                    continue;
                }

                amounts[item.Source] += item.AmountUsd;
                counts[item.Source]++;
                if (item.MissingUsd)
                {
                    missing[item.Source]++;
                }
            }

            var grand = amounts.Values.Sum();
            var totals = Sources.Select(s => new RevenueSourceTotal
            {
                Source = s,
                AmountUsd = Math.Round(amounts[s], decimals), // in XMR when the report was built from XMR amounts
                Count = counts[s],
                MissingUsdCount = missing[s],
                SharePct = grand > 0m ? Math.Round(100m * amounts[s] / grand, 1) : 0m,
            })
            .OrderByDescending(t => t.AmountUsd) // largest share first (table, pie and legend)
            .ToList();

            return new RevenueReport
            {
                FromUtc = from,
                ToUtc = to,
                Basis = basis,
                Totals = totals,
                Months = BuildMonths(sponsorInvoices, oneTime, from, to, basis, nowUtc),
            };
        }

        // Monthly series over [fromUtc, toUtc), one entry per calendar month (partial at the ends).
        public static List<RevenueMonth> BuildMonths(
            IReadOnlyList<SponsoredListingInvoice> sponsorInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            DateTime fromUtc,
            DateTime toUtc,
            RevenueBasis basis,
            DateTime nowUtc)
        {
            var months = new List<RevenueMonth>();
            var cursor = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            while (cursor < toUtc)
            {
                var next = cursor.AddMonths(1);
                var mFrom = cursor < fromUtc ? fromUtc : cursor;
                var mTo = next > toUtc ? toUtc : next;

                var bySource = Sources.ToDictionary(s => s, _ => 0m);
                foreach (var inv in sponsorInvoices)
                {
                    bySource[RevenueSource.SiteSponsors] += SponsorAmount(inv, mFrom, mTo, basis, nowUtc);
                }

                foreach (var item in oneTime)
                {
                    if (item.PaidUtc >= mFrom && item.PaidUtc < mTo)
                    {
                        bySource[item.Source] += item.AmountUsd;
                    }
                }

                months.Add(new RevenueMonth { Month = cursor, BySource = bySource });
                cursor = next;
            }

            return months;
        }

        /// <summary>
        /// Average revenue per month over [fromUtc, toUtc), never past now. Always on the EARNED
        /// basis: each sponsorship package counts a little every day it runs (amount / days paid
        /// for), so a year paid up front weighs the same as twelve single months. The total is
        /// divided by the time that has actually passed (in average-length months), so the
        /// unfinished current month counts only for the days gone by.
        /// </summary>
        /// <param name="fromFirstPayment">Start the clock at the first month with revenue ("All time").</param>
        /// <returns>The average per month, or 0 when there is nothing to average.</returns>
        public static decimal AverageEarnedPerMonth(
            IReadOnlyList<SponsoredListingInvoice> sponsorInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime nowUtc,
            bool fromFirstPayment = false)
        {
            var end = toUtc > nowUtc ? nowUtc : toUtc;
            var start = fromUtc;
            if (end <= start)
            {
                return 0m;
            }

            var months = BuildMonths(sponsorInvoices, oneTime, start, end, RevenueBasis.Earned, nowUtc);
            if (fromFirstPayment)
            {
                months = TrimLeadingEmptyMonths(months);
                if (months.Count == 0)
                {
                    return 0m;
                }

                if (months[0].Month > start)
                {
                    start = months[0].Month;
                }
            }

            const double DaysPerMonth = 365.25 / 12;
            var elapsedMonths = (decimal)((end - start).TotalDays / DaysPerMonth);
            return elapsedMonths <= 0m ? 0m : months.Sum(m => m.Total) / elapsedMonths;
        }

        /// <summary>
        /// The average earned per month as it stood at the end of each month: everything earned
        /// from the start through that month, divided by the time passed until then. One point per
        /// month, so the line shows whether the monthly average is climbing or falling; its last
        /// point is the overall average (<see cref="AverageEarnedPerMonth"/>). Same rules: earned
        /// basis, never past now, current month counted only for the days gone by.
        /// </summary>
        /// <returns>One (month, average so far) pair per month, oldest first.</returns>
        public static List<(DateTime Month, decimal Average)> RunningAverageEarnedPerMonth(
            IReadOnlyList<SponsoredListingInvoice> sponsorInvoices,
            IReadOnlyList<RevenueItem> oneTime,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime nowUtc,
            bool fromFirstPayment = false)
        {
            var result = new List<(DateTime Month, decimal Average)>();
            var end = toUtc > nowUtc ? nowUtc : toUtc;
            var start = fromUtc;
            if (end <= start)
            {
                return result;
            }

            var months = BuildMonths(sponsorInvoices, oneTime, start, end, RevenueBasis.Earned, nowUtc);
            if (fromFirstPayment)
            {
                months = TrimLeadingEmptyMonths(months);
                if (months.Count > 0 && months[0].Month > start)
                {
                    start = months[0].Month;
                }
            }

            const double DaysPerMonth = 365.25 / 12;
            decimal earnedSoFar = 0m;
            foreach (var m in months)
            {
                earnedSoFar += m.Total;
                var monthEnd = m.Month.AddMonths(1);
                var until = monthEnd > end ? end : monthEnd;
                var elapsedMonths = (decimal)((until - start).TotalDays / DaysPerMonth);
                if (elapsedMonths > 0m)
                {
                    result.Add((m.Month, earnedSoFar / elapsedMonths));
                }
            }

            return result;
        }

        /// <summary>Drops the empty months before the first payment (so "All time" starts there).</summary>
        public static List<RevenueMonth> TrimLeadingEmptyMonths(IReadOnlyList<RevenueMonth> months) =>
            months.SkipWhile(m => m.Total <= 0m).ToList();

        private static decimal SponsorAmount(SponsoredListingInvoice inv, DateTime from, DateTime to, RevenueBasis basis, DateTime nowUtc)
        {
            if (basis == RevenueBasis.Cash)
            {
                return inv.CreateDate >= from && inv.CreateDate < to ? SponsorAccrual.UsdAmount(inv) : 0m;
            }

            return SponsorAccrual.EarnedBetween(inv, from, to, nowUtc);
        }
    }
}
