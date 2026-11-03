namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Average order size from verified reviews. Reviews carry an amount BAND tag
    /// ("$1K-5K", "Under $50", "Over $100K"), not an exact figure, so each order counts as the
    /// middle of its band; open-ended bands count at their one known edge. The result is an
    /// estimate and is shown as "about".
    /// </summary>
    public static class VerifiedOrderAmounts
    {
        /// <summary>Only show an average once at least this many approved reviews state an amount.</summary>
        public const int MinOrdersToShow = 2;

        public static (int Count, decimal? Average) Summarize(IEnumerable<(decimal? Min, decimal? Max)> bands)
        {
            var mids = (bands ?? Enumerable.Empty<(decimal? Min, decimal? Max)>())
                .Where(b => b.Min.HasValue || b.Max.HasValue)
                .Select(b => b.Min.HasValue && b.Max.HasValue ? (b.Min.Value + b.Max.Value) / 2m
                    : b.Max.HasValue ? b.Max.Value / 2m      // "Under $X": middle of 0..X
                    : b.Min!.Value)                          // "Over $X": at least X
                .ToList();

            return mids.Count == 0 ? (0, null) : (mids.Count, mids.Average());
        }

        // The same amount buckets as the review tags (lower bound inclusive, upper exclusive).
        private static readonly (decimal Min, string Label)[] Buckets =
        {
            (100_000m, "Over $100K"), (50_000m, "$50K-100K"), (20_000m, "$20K-50K"), (10_000m, "$10K-20K"),
            (5_000m, "$5K-10K"), (1_000m, "$1K-5K"), (500m, "$500-1K"), (200m, "$200-500"), (50m, "$50-200"), (0m, "Under $50"),
        };

        /// <summary>The review-tag bucket the average falls in, e.g. "$10K-20K".</summary>
        public static string Bucket(decimal usd) => Buckets.First(b => usd >= b.Min).Label;

        /// <summary>"$66", "$2,320", "$19,000" (rounded to a sensible precision for an estimate).</summary>
        public static string Format(decimal usd)
        {
            var rounded = usd >= 10_000m ? Math.Round(usd / 100m) * 100m
                : usd >= 1_000m ? Math.Round(usd / 10m) * 10m
                : Math.Round(usd);
            return "$" + rounded.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
