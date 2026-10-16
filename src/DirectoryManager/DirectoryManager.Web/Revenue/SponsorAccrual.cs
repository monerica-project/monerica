using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Revenue
{
    // Turns a paid sponsorship invoice into revenue EARNED over time: the amount is spread evenly
    // across the days it pays for, so a year bought up front counts as 1/365 per day instead of
    // one big spike on the day it was paid.
    //
    // A sponsorship that was cut short (the listing's campaign was shortened, or the advertiser
    // was pulled while still paid up) earns at its daily rate until it stopped, and the money kept
    // for the unserved remainder is counted on the day it stopped.
    public static class SponsorAccrual
    {
        // The invoice's USD amount (invoices are priced in USD).
        public static decimal UsdAmount(SponsoredListingInvoice inv) => inv.AmountIn(Currency.USD);

        public static decimal DailyRate(SponsoredListingInvoice inv)
        {
            var days = PaidDays(inv);
            return days <= 0 ? 0m : UsdAmount(inv) / days;
        }

        public static int PaidDays(SponsoredListingInvoice inv) =>
            Math.Max(1, (int)Math.Round((inv.CampaignEndDate.Date - inv.CampaignStartDate.Date).TotalDays));

        // When service actually stopped: the paid end, or earlier if the sponsorship was cut short.
        public static DateTime ServedEnd(SponsoredListingInvoice inv, DateTime nowUtc)
        {
            var paidEnd = inv.CampaignEndDate.Date;
            var listingEnd = inv.SponsoredListing?.CampaignEndDate.Date;
            if (listingEnd.HasValue && listingEnd.Value < paidEnd.AddDays(-1) && listingEnd.Value >= inv.CampaignStartDate.Date)
            {
                return listingEnd.Value;
            }

            var status = inv.DirectoryEntry?.DirectoryStatus;
            if (status.HasValue && !SponsorshipIncomeAnalyzer.IsLiveAdvertiser(status.Value) && paidEnd > nowUtc.Date)
            {
                return nowUtc.Date < inv.CampaignStartDate.Date ? inv.CampaignStartDate.Date : nowUtc.Date;
            }

            return paidEnd;
        }

        // Revenue this invoice earned in [fromUtc, toUtc). Only days up to nowUtc count as earned;
        // later days are still owed service (that's what the forecast projects).
        public static decimal EarnedBetween(SponsoredListingInvoice inv, DateTime fromUtc, DateTime toUtc, DateTime nowUtc)
        {
            var from = fromUtc.Date;
            var to = toUtc.Date;
            var today = nowUtc.Date.AddDays(1); // today counts as earned
            if (to > today)
            {
                to = today;
            }

            if (to <= from)
            {
                return 0m;
            }

            var start = inv.CampaignStartDate.Date;
            var servedEnd = ServedEnd(inv, nowUtc);
            var rate = DailyRate(inv);

            decimal earned = 0m;
            var overlapStart = start > from ? start : from;
            var overlapEnd = servedEnd < to ? servedEnd : to;
            if (overlapEnd > overlapStart)
            {
                earned += rate * (decimal)(overlapEnd - overlapStart).TotalDays;
            }

            // Money kept for a cut-short remainder is recognized on the day service stopped.
            var paidEnd = inv.CampaignEndDate.Date;
            if (servedEnd < paidEnd && servedEnd >= from && servedEnd < to)
            {
                var servedDays = Math.Max(0.0, (servedEnd - start).TotalDays);
                earned += Math.Max(0m, UsdAmount(inv) - (rate * (decimal)servedDays));
            }

            return earned;
        }
    }
}
