namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Suggested market price for one sponsorship placement type, derived from paid history by
    /// <see cref="MarketPriceEstimator"/>.
    /// </summary>
    public sealed class MarketPriceEstimate
    {
        public bool HasData { get; set; }

        /// <summary>The recommended going rate per day (recent median, or overall median if no recent sales).</summary>
        public decimal SuggestedPerDay { get; set; }

        /// <summary>Suggested price for a full campaign at the typical purchased length.</summary>
        public decimal SuggestedPerCampaign { get; set; }

        public decimal MedianPerDay { get; set; }

        public decimal P25PerDay { get; set; }

        public decimal P75PerDay { get; set; }

        public bool HasRecent { get; set; }

        public decimal RecentPerDay { get; set; }

        /// <summary>Recent going rate vs the earlier part of the range, in percent (null = not enough data).</summary>
        public double? TrendPercent { get; set; }

        public int TypicalCampaignDays { get; set; }

        public int PaidInvoices { get; set; }

        public long ActiveListingDays { get; set; }
    }
}
