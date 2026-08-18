using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Turns historical PAID invoices into a suggested "market price" for a sponsorship slot of a
    /// given placement type. Works off the per-day rate each advertiser actually paid
    /// (amount / campaign days), weighted by how many days that campaign was active within the
    /// selected range — so a long-running campaign influences the going rate more than a one-day
    /// one. Uses the MEDIAN (robust to the odd outlier) for the headline numbers, the 25th–75th
    /// percentile for a "typical range", and the most recent window for the recommendation so the
    /// price tracks the current market rather than being dragged by old sales.
    /// </summary>
    public static class MarketPriceEstimator
    {
        public static MarketPriceEstimate Estimate(
            IEnumerable<SponsoredListingInvoice> paidInvoices,
            SponsorshipType type,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            int recentDays = 90)
        {
            var estimate = new MarketPriceEstimate();

            var rStart = rangeStart.Date;
            var rEnd = rangeEnd.Date;
            if (rEnd < rStart)
            {
                return estimate;
            }

            var recentStart = rEnd.AddDays(-(recentDays - 1));
            if (recentStart < rStart)
            {
                recentStart = rStart;
            }

            // (rate, daysInRange, recentDays, campaignDays) per contributing invoice.
            var all = new List<(decimal Rate, long Days)>();
            var recent = new List<(decimal Rate, long Days)>();
            var earlier = new List<(decimal Rate, long Days)>();
            var campaignLengths = new List<int>();
            long activeListingDays = 0;

            foreach (var inv in paidInvoices ?? Enumerable.Empty<SponsoredListingInvoice>())
            {
                if (inv.SponsorshipType != type)
                {
                    continue;
                }

                var amount = inv.AmountIn(displayCurrency);
                if (amount <= 0m)
                {
                    continue;
                }

                var cs = inv.CampaignStartDate.Date;
                var ce = inv.CampaignEndDate.Date;
                if (ce < cs)
                {
                    continue;
                }

                var campaignDays = InclusiveDays(cs, ce);
                if (campaignDays <= 0)
                {
                    continue;
                }

                var overlap = InclusiveDays(Max(cs, rStart), Min(ce, rEnd));
                if (overlap <= 0)
                {
                    continue;
                }

                var recentOverlap = InclusiveDays(Max(cs, recentStart), Min(ce, rEnd));
                if (recentOverlap < 0)
                {
                    recentOverlap = 0;
                }

                var rate = amount / campaignDays;

                all.Add((rate, overlap));
                activeListingDays += overlap;
                campaignLengths.Add(campaignDays);

                if (recentOverlap > 0)
                {
                    recent.Add((rate, recentOverlap));
                }

                var earlierOverlap = overlap - recentOverlap;
                if (earlierOverlap > 0)
                {
                    earlier.Add((rate, earlierOverlap));
                }
            }

            if (all.Count == 0)
            {
                return estimate;
            }

            estimate.HasData = true;
            estimate.PaidInvoices = all.Count;
            estimate.ActiveListingDays = activeListingDays;

            estimate.MedianPerDay = WeightedQuantile(all, 0.5);
            estimate.P25PerDay = WeightedQuantile(all, 0.25);
            estimate.P75PerDay = WeightedQuantile(all, 0.75);

            var recentWeight = recent.Sum(x => x.Days);
            estimate.HasRecent = recentWeight > 0;
            estimate.RecentPerDay = estimate.HasRecent ? WeightedQuantile(recent, 0.5) : 0m;

            var earlierWeight = earlier.Sum(x => x.Days);
            var earlierMedian = earlierWeight > 0 ? WeightedQuantile(earlier, 0.5) : (decimal?)null;

            // Recommendation: the recent going rate if we have recent sales, else the overall median.
            estimate.SuggestedPerDay = estimate.HasRecent ? estimate.RecentPerDay : estimate.MedianPerDay;

            // Trend: recent vs the earlier portion of the same range.
            if (estimate.HasRecent && earlierMedian.HasValue && earlierMedian.Value > 0m)
            {
                estimate.TrendPercent = (double)((estimate.RecentPerDay - earlierMedian.Value) / earlierMedian.Value) * 100.0;
            }

            estimate.TypicalCampaignDays = MedianInt(campaignLengths);
            estimate.SuggestedPerCampaign = estimate.SuggestedPerDay * estimate.TypicalCampaignDays;

            return estimate;
        }

        private static int InclusiveDays(DateTime start, DateTime end)
        {
            var span = end.Date.AddDays(1) - start.Date;
            var days = (int)span.TotalDays;
            return Math.Max(0, days);
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        // Lower-weighted-quantile: sort by value, walk the cumulative weight to q * total.
        private static decimal WeightedQuantile(List<(decimal Rate, long Days)> samples, double q)
        {
            if (samples.Count == 0)
            {
                return 0m;
            }

            var sorted = samples.OrderBy(s => s.Rate).ToList();
            long total = sorted.Sum(s => s.Days);
            if (total <= 0)
            {
                return sorted[0].Rate;
            }

            double target = q * total;
            long cumulative = 0;
            foreach (var s in sorted)
            {
                cumulative += s.Days;
                if (cumulative >= target)
                {
                    return s.Rate;
                }
            }

            return sorted[sorted.Count - 1].Rate;
        }

        private static int MedianInt(List<int> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            if (sorted.Count % 2 == 1)
            {
                return sorted[mid];
            }

            return (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
        }
    }
}
