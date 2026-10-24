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
            }).ToList();

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
