// ChurnMetricsPoint.cs
using System;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Services.Models.TransferModels
{
    /// <summary>
    /// A single point in a churn time series (typically calendar month granularity).
    /// </summary>
    public sealed class ChurnMetricsPoint
    {
        /// <summary>
        /// Gets or sets the inclusive period start (UTC). For monthly series, this is the month start.
        /// </summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the exclusive period end (UTC). For monthly series, this is the next month start.
        /// </summary>
        public DateTime PeriodEndOpenUtc { get; set; }

        /// <summary>
        /// Gets or sets the optional sponsorship type filter used to compute this point.
        /// </summary>
        public SponsorshipType? SponsorshipType { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers active at <see cref="PeriodStartUtc"/>.
        /// </summary>
        public int ActiveAtStart { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers that activated during the period.
        /// </summary>
        public int ActivatedInPeriod { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers that churned during the period.
        /// </summary>
        public int ChurnedInPeriod { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers active at the end of the period.
        /// </summary>
        public int ActiveAtEnd { get; set; }

        /// <summary>
        /// Gets or sets the churn rate for the period (typically ChurnedInPeriod / max(ActiveAtStart, 1)).
        /// </summary>
        public decimal ChurnRate { get; set; }
    }
}
