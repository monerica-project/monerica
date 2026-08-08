using System;
using System.Collections.Generic;
using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>
    /// View model for the income forecasting report.
    /// </summary>
    public class IncomeForecastViewModel
    {
        // ---- Inputs (round-tripped through the form) ----
        public Currency DisplayCurrency { get; set; } = Currency.USD;
        public List<SelectListItem> DisplayCurrencyOptions { get; set; } = new ();

        /// <summary>How many months of history to learn the trend from.</summary>
        public int LookbackMonths { get; set; } = 24;

        /// <summary>How many months to project forward (current month counts as the first).</summary>
        public int HorizonMonths { get; set; } = 12;

        /// <summary>Confidence level for the band, as a percent (e.g. 80).</summary>
        public int ConfidencePercent { get; set; } = 80;
        public List<SelectListItem> ConfidenceOptions { get; set; } = new ();

        // ---- Outputs ----
        public bool HasEnoughData { get; set; }
        public int HistoryMonthsUsed { get; set; }
        public DateTime GeneratedAtUtc { get; set; }

        /// <summary>Average of the most recent (up to 3) completed months.</summary>
        public decimal RecentMonthlyAverage { get; set; }

        /// <summary>Trend slope: change in monthly income per month. Positive = growing.</summary>
        public decimal TrendSlopePerMonth { get; set; }

        public decimal ProjectedHorizonTotalExpected { get; set; }
        public decimal ProjectedHorizonTotalLow { get; set; }
        public decimal ProjectedHorizonTotalHigh { get; set; }

        public List<ForecastMonthRow> MonthlyRows { get; set; } = new ();
        public List<ForecastMilestoneRow> Milestones { get; set; } = new ();

        // ---- Next concrete expected payment ----
        // Derived from the soonest-expiring active paid sponsorship (the date its current
        // campaign ends and a renewal would be due), at that listing's current rate.
        // Only advertisers who are still Admitted/Verified are considered — a pulled
        // advertiser (e.g. now Removed/Scam) is not assumed to renew.
        public bool HasNextPayment { get; set; }
        public DateTime? NextPaymentDateUtc { get; set; }
        public decimal NextPaymentAmount { get; set; }
        public string NextPaymentAdvertiser { get; set; } = string.Empty;
        public string NextPaymentSponsorshipType { get; set; } = string.Empty;

        /// <summary>True when a sooner-expiring sponsorship exists but was excluded because its
        /// advertiser is no longer Admitted/Verified (so it shouldn't be forecast as a renewal).</summary>
        public bool NextPaymentExcludedTerminated { get; set; }

        // ---- Realistic monthly income (the headline the owner actually wants) ----
        public decimal AverageMonthly3 { get; set; }
        public decimal AverageMonthly6 { get; set; }
        public decimal AverageMonthly12 { get; set; }

        /// <summary>Average expected monthly income across the whole forecast horizon.</summary>
        public decimal ExpectedMonthlyOverHorizon { get; set; }

        /// <summary>Forward recurring run-rate: sum of current live sponsorships' monthly-equivalent
        /// value (amount ÷ campaign length), across advertisers still Admitted/Verified.</summary>
        public decimal ActiveMonthlyRunRate { get; set; }
        public int ActiveSponsorshipCount { get; set; }
        public List<ActiveSponsorRow> ActiveSponsors { get; set; } = new ();

        // ---- Advertiser health ----
        public int DistinctAdvertisers { get; set; }
        public int LiveAdvertisers { get; set; }
        public int TerminatedAdvertisers { get; set; }
        public decimal ChurnRatePct { get; set; }
        public decimal RepeatAdvertiserRatePct { get; set; }

        // ---- Pricing drift (trailing 6mo avg price vs the 6mo before it) ----
        public decimal AvgPriceRecent { get; set; }
        public decimal AvgPricePrior { get; set; }
        public decimal PriceChangePct { get; set; }

        // ---- Cut-short revenue (collected before expiration was supposed to happen) ----
        public int CutShortCount { get; set; }
        public decimal CutShortCollectedTotal { get; set; }
        public decimal CutShortUnearnedTotal { get; set; }
        public List<CutShortRow> CutShortRows { get; set; } = new ();

        // ---- Actionable advice ----
        public List<ForecastAdvice> Advice { get; set; } = new ();

        public bool TrendIsGrowing => this.TrendSlopePerMonth > 0m;
    }
}
