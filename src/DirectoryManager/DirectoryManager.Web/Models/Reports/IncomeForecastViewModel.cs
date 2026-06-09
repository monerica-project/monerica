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
        public bool HasNextPayment { get; set; }
        public DateTime? NextPaymentDateUtc { get; set; }
        public decimal NextPaymentAmount { get; set; }
        public string NextPaymentAdvertiser { get; set; } = string.Empty;
        public string NextPaymentSponsorshipType { get; set; } = string.Empty;

        public bool TrendIsGrowing => this.TrendSlopePerMonth > 0m;
    }
}
