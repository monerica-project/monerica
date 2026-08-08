using System;
using System.Collections.Generic;
using System.Linq;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Forecasting
{
    /// <summary>
    /// Turns the raw paid-invoice set into a realistic income picture:
    ///  - separates <b>durable / recurring</b> revenue from <b>cut-short</b> revenue (money kept for
    ///    a campaign that was terminated early because the advertiser misbehaved and was pulled),
    ///  - measures how much was "collected before expiration was supposed to happen" (unearned),
    ///  - computes a forward <b>run-rate</b> from advertisers that are still Admitted/Verified,
    ///  - and derives pricing signals (churn, repeat rate, price drift) for actionable advice.
    ///
    /// Cut-short revenue and revenue tied to advertisers who are no longer live are treated as
    /// one-off, so the trend model isn't inflated by money that will not recur.
    /// </summary>
    public static class SponsorshipIncomeAnalyzer
    {
        private const double DaysPerMonth = 30.4375;

        public static bool IsLiveAdvertiser(DirectoryStatus s) =>
            s == DirectoryStatus.Admitted || s == DirectoryStatus.Verified;

        public sealed class CutShortItem
        {
            public string Advertiser { get; set; } = string.Empty;
            public DirectoryStatus Status { get; set; }
            public string SponsorshipType { get; set; } = string.Empty;
            public DateTime PaidThrough { get; set; }
            public DateTime ServedThrough { get; set; }
            public decimal Collected { get; set; }
            public decimal Unearned { get; set; }
        }

        public sealed class ActiveSponsorItem
        {
            public string Advertiser { get; set; } = string.Empty;
            public string SponsorshipType { get; set; } = string.Empty;
            public DateTime EndsOn { get; set; }
            public decimal Amount { get; set; }
            public decimal MonthlyValue { get; set; }
        }

        public sealed class Result
        {
            // Advertiser health
            public int DistinctAdvertisers { get; set; }
            public int LiveAdvertisers { get; set; }
            public int TerminatedAdvertisers { get; set; }
            public decimal ChurnRatePct { get; set; }
            public decimal RepeatAdvertiserRatePct { get; set; }

            // Forward recurring run-rate (current live sponsorships only)
            public decimal ActiveMonthlyRunRate { get; set; }
            public int ActiveSponsorshipCount { get; set; }
            public List<ActiveSponsorItem> ActiveSponsors { get; set; } = new ();

            // Cut-short (one-off, excluded from the trend)
            public int CutShortCount { get; set; }
            public decimal CutShortCollectedTotal { get; set; }
            public decimal CutShortUnearnedTotal { get; set; }
            public List<CutShortItem> CutShortItems { get; set; } = new ();

            // Unearned cut-short amount, keyed by the month the invoice was booked (first-of-month, UTC),
            // so the caller can subtract it from that month's gross before fitting the trend.
            public Dictionary<DateTime, decimal> UnearnedByMonth { get; set; } = new ();

            // Pricing drift
            public decimal AvgPriceRecent { get; set; }
            public decimal AvgPricePrior { get; set; }
            public decimal PriceChangePct { get; set; }
        }

        public static Result Analyze(IReadOnlyList<SponsoredListingInvoice> paid, Currency currency, DateTime now)
        {
            var res = new Result();
            if (paid == null || paid.Count == 0)
            {
                return res;
            }

            var nowDate = now.Date;

            // ---- advertiser buckets ----
            var byAdvertiser = paid.GroupBy(i => i.DirectoryEntryId).ToList();
            res.DistinctAdvertisers = byAdvertiser.Count;
            foreach (var g in byAdvertiser)
            {
                var status = g.Select(x => x.DirectoryEntry?.DirectoryStatus ?? DirectoryStatus.Unknown).First();
                if (IsLiveAdvertiser(status))
                {
                    res.LiveAdvertisers++;
                }
                else
                {
                    res.TerminatedAdvertisers++;
                }
            }

            res.ChurnRatePct = res.DistinctAdvertisers > 0
                ? Math.Round(100m * res.TerminatedAdvertisers / res.DistinctAdvertisers, 1)
                : 0m;
            var repeat = byAdvertiser.Count(g => g.Count() >= 2);
            res.RepeatAdvertiserRatePct = res.DistinctAdvertisers > 0
                ? Math.Round(100m * repeat / res.DistinctAdvertisers, 1)
                : 0m;

            // ---- per-invoice cut-short analysis ----
            foreach (var inv in paid)
            {
                var amount = inv.AmountIn(currency);
                var start = inv.CampaignStartDate;
                var paidEnd = inv.CampaignEndDate;
                var status = inv.DirectoryEntry?.DirectoryStatus ?? DirectoryStatus.Unknown;
                var listingEnd = inv.SponsoredListing?.CampaignEndDate;

                var totalDays = Math.Max(1.0, (paidEnd - start).TotalDays);
                var servedEnd = paidEnd;
                var cutShort = false;

                // Signal 1: the live listing was explicitly shortened below what was paid for.
                if (listingEnd.HasValue && listingEnd.Value.Date < paidEnd.Date.AddDays(-1))
                {
                    servedEnd = listingEnd.Value;
                    cutShort = true;
                }
                else if (!IsLiveAdvertiser(status) && paidEnd.Date > nowDate)
                {
                    // Signal 2: advertiser is no longer Admitted/Verified yet paid through a future
                    // date -> they were pulled; treat service as ending ~now.
                    servedEnd = now;
                    cutShort = true;
                }

                if (!cutShort)
                {
                    continue;
                }

                var unservedDays = Math.Max(0.0, (paidEnd - servedEnd).TotalDays);
                var unearned = amount * (decimal)Math.Clamp(unservedDays / totalDays, 0.0, 1.0);

                res.CutShortCount++;
                res.CutShortCollectedTotal += amount;
                res.CutShortUnearnedTotal += unearned;

                var monthKey = new DateTime(inv.CreateDate.Year, inv.CreateDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                res.UnearnedByMonth[monthKey] = res.UnearnedByMonth.GetValueOrDefault(monthKey) + unearned;

                res.CutShortItems.Add(new CutShortItem
                {
                    Advertiser = inv.DirectoryEntry?.Name ?? "(unknown advertiser)",
                    Status = status,
                    SponsorshipType = inv.SponsorshipType.ToString(),
                    PaidThrough = paidEnd,
                    ServedThrough = servedEnd,
                    Collected = amount,
                    Unearned = unearned,
                });
            }

            res.CutShortItems = res.CutShortItems
                .OrderByDescending(x => x.Unearned)
                .ThenByDescending(x => x.Collected)
                .ToList();

            // ---- forward run-rate: latest live sponsorship per advertiser+slot, still running ----
            var currentActive = paid
                .Where(i => IsLiveAdvertiser(i.DirectoryEntry?.DirectoryStatus ?? DirectoryStatus.Unknown))
                .GroupBy(i => new { i.DirectoryEntryId, i.SponsorshipType, i.CategoryId, i.SubCategoryId })
                .Select(g => g.OrderByDescending(x => x.CampaignEndDate).First())
                .Where(latest => latest.CampaignEndDate.Date >= nowDate)
                .ToList();

            foreach (var inv in currentActive)
            {
                var amount = inv.AmountIn(currency);
                var months = Math.Max(1.0, (inv.CampaignEndDate - inv.CampaignStartDate).TotalDays / DaysPerMonth);
                var monthly = amount / (decimal)months;

                res.ActiveSponsorshipCount++;
                res.ActiveMonthlyRunRate += monthly;
                res.ActiveSponsors.Add(new ActiveSponsorItem
                {
                    Advertiser = inv.DirectoryEntry?.Name ?? "(unknown advertiser)",
                    SponsorshipType = inv.SponsorshipType.ToString(),
                    EndsOn = inv.CampaignEndDate,
                    Amount = amount,
                    MonthlyValue = monthly,
                });
            }

            res.ActiveMonthlyRunRate = Math.Round(res.ActiveMonthlyRunRate, 2);
            res.ActiveSponsors = res.ActiveSponsors.OrderByDescending(x => x.MonthlyValue).ToList();

            // ---- price drift: avg amount per sponsorship, trailing 6mo vs the 6mo before that ----
            var recent = paid.Where(i => i.CreateDate >= now.AddMonths(-6)).ToList();
            var prior = paid.Where(i => i.CreateDate < now.AddMonths(-6) && i.CreateDate >= now.AddMonths(-12)).ToList();
            res.AvgPriceRecent = recent.Count > 0 ? Math.Round(recent.Average(i => i.AmountIn(currency)), 2) : 0m;
            res.AvgPricePrior = prior.Count > 0 ? Math.Round(prior.Average(i => i.AmountIn(currency)), 2) : 0m;
            res.PriceChangePct = res.AvgPricePrior > 0
                ? Math.Round(100m * (res.AvgPriceRecent - res.AvgPricePrior) / res.AvgPricePrior, 1)
                : 0m;

            return res;
        }
    }
}
