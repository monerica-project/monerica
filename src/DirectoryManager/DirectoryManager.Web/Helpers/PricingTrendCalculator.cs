using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Computes, for a date range, the AVERAGE PER-DAY price that advertisers actually PAID for a
    /// sponsorship slot, broken out by placement type (Main / Category / Subcategory) and bucketed
    /// to individual calendar days.
    ///
    /// Method: each paid invoice covers a campaign window and paid a total <c>Amount</c>. Its
    /// per-day rate is <c>Amount / campaignDays</c>. A day's value for a placement type is the MEAN
    /// of the per-day rates of every paid invoice of that type whose campaign is active on that day
    /// — i.e. "what did a slot of this type cost per day, on average, on this date". Days with no
    /// active paid campaign of a type are left as <see cref="double.NaN"/> (a gap, not a zero, so
    /// the average is never diluted by days nobody paid).
    /// </summary>
    public static class PricingTrendCalculator
    {
        public static readonly SponsorshipType[] Types =
        {
            SponsorshipType.MainSponsor,
            SponsorshipType.CategorySponsor,
            SponsorshipType.SubcategorySponsor,
        };

        public static PricingTrendResult Build(
            IEnumerable<SponsoredListingInvoice> paidInvoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd)
        {
            var result = new PricingTrendResult();

            var start = rangeStart.Date;
            var end = rangeEnd.Date;
            if (end < start)
            {
                return result;
            }

            // Inclusive day list for the range.
            var days = new List<DateTime>();
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                days.Add(d);
            }

            result.Days = days;

            var invoices = (paidInvoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();

            // Pre-compute each invoice's per-day rate + campaign span once.
            var priced = new List<(SponsorshipType Type, DateTime Start, DateTime End, decimal PerDay)>();
            foreach (var inv in invoices)
            {
                if (!Types.Contains(inv.SponsorshipType))
                {
                    continue;
                }

                var amount = inv.AmountIn(displayCurrency);
                if (amount <= 0m)
                {
                    continue;
                }

                var s = inv.CampaignStartDate.Date;
                var e = inv.CampaignEndDate.Date;
                if (e < s)
                {
                    continue;
                }

                var span = e.AddDays(1) - s;
                var campaignDays = (int)span.TotalDays;
                if (campaignDays <= 0)
                {
                    continue;
                }

                priced.Add((inv.SponsorshipType, s, e, amount / campaignDays));
            }

            foreach (var type in Types)
            {
                var series = new double[days.Count];
                var ofType = priced.Where(p => p.Type == type).ToList();

                var contributingInvoices = 0;
                long activeListingDays = 0;
                var daysWithData = 0;
                decimal sumOfDailyAverages = 0m;
                decimal? min = null;
                decimal? max = null;

                // Count invoices that overlap the range at all (for the summary).
                foreach (var p in ofType)
                {
                    if (p.End >= start && p.Start <= end)
                    {
                        contributingInvoices++;
                    }
                }

                for (var i = 0; i < days.Count; i++)
                {
                    var day = days[i];
                    decimal sum = 0m;
                    var n = 0;
                    foreach (var p in ofType)
                    {
                        if (p.Start <= day && day <= p.End)
                        {
                            sum += p.PerDay;
                            n++;
                        }
                    }

                    if (n == 0)
                    {
                        series[i] = double.NaN;
                        continue;
                    }

                    var avg = sum / n;
                    series[i] = (double)avg;
                    activeListingDays += n;
                    daysWithData++;
                    sumOfDailyAverages += avg;
                    min = min.HasValue ? Math.Min(min.Value, avg) : avg;
                    max = max.HasValue ? Math.Max(max.Value, avg) : avg;
                }

                result.DailyValues[type] = series;
                result.Stats[type] = new PricingTrendStat
                {
                    AvgPerDay = daysWithData > 0 ? sumOfDailyAverages / daysWithData : 0m,
                    MinPerDay = min ?? 0m,
                    MaxPerDay = max ?? 0m,
                    DaysWithData = daysWithData,
                    PaidInvoices = contributingInvoices,
                    ActiveListingDays = activeListingDays,
                };
            }

            return result;
        }
    }
}
