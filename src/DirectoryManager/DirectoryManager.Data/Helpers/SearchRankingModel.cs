using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// The search ranking model, in one place. Results are ordered by these keys, in order:
    ///
    /// 1. Sink: Questionable listings go after everything else, unless the search names them.
    ///    (Scam and Removed listings are not shown at all, unless the search names the listing.)
    /// 2. Name protection: an exact name match, then a "name starts with the search" match, always
    ///    come first, so searching for a business shows that business first no matter how many
    ///    reviews another listing has.
    /// 3. Words matched (only differs when nothing matched every word).
    /// 4. Score = tier band + text relevance (0..<see cref="MaxRelevance"/>) + quality
    ///    (<see cref="MinQuality"/>..<see cref="MaxQuality"/>). The bands are spaced so quality can
    ///    move a listing between adjacent non-name tiers (Category/Text, or Taxonomy/Category when
    ///    the other listing has penalties), and can never lift anything above a name match.
    /// 5. Status (Verified, Admitted, Questionable), then name, then id: a stable final order.
    ///
    /// Generic searches: when every word of the search is a category/subcategory/tag word ("vpn",
    /// "esim", "gift cards"), listings whose NAME merely contains the word ("VPS Price Tracker")
    /// are put in the same band as listings in that category, so quality decides the order instead
    /// of the wording of the name. Exact names are still protected.
    ///
    /// Text relevance counts each field once (presence, not how often a word is repeated).
    /// Sponsorship and affiliate status are deliberately NOT inputs: paid placement is handled
    /// only by the results page (sponsors are moved to the top of their page, as before).
    /// </summary>
    public static class SearchRankingModel
    {
        // ---- Tier bands (score points). Gaps around the name tiers (180+) exceed the whole quality
        // range (120), so quality never reorders name tiers. Taxonomy/Category are 80 apart and
        // Category/Text 30 apart, so there quality can pass a weaker or penalized listing.
        public const double BandNameWord = 600;
        public const double BandNameContains = 420;
        public const double BandTaxonomy = 200;
        public const double BandCategory = 120;
        public const double BandText = 90;
        public const double BandPartial = 0;

        /// <summary>Text relevance adds up to this many points within a band (name 1.0 … description 0.15 per word).</summary>
        public const double MaxRelevance = 20;

        // ---- Quality, clamped to this range.
        public const double MinQuality = -60;
        public const double MaxQuality = 60;

        // Status: Verified listings were checked by the site; Unknown ones never were.
        public const double VerifiedPoints = 25;
        public const double AdmittedPoints = 0;
        public const double UnknownStatusPoints = -5;
        public const double QuestionablePoints = -40; // also sinks (an open incident)
        public const double ScamPoints = -60;         // only ever shown when searched by name

        // Reviews. Bayesian average: (v*R + m*C) / (v + m) with the site mean C and a prior of m
        // reviews, so one 5-star review barely moves a listing while forty 4.6-star reviews do.
        public const double DefaultMeanRating = 4.3;
        public const double RatingPrior = 3;            // m
        public const double PointsPerStarAboveMean = 15; // (bayes - C) * this
        public const double MaxRatingPoints = 15;

        // Review volume: diminishing returns (log scale), only for listings people rate well.
        public const double VolumePointsPerLog = 6;  // 6 * ln(1 + n): 1 → 4, 10 → 14, 40 → 22
        public const double MaxVolumePoints = 22;
        public const double MinRatingForVolume = 3.5; // a pile of bad reviews earns no volume points

        // Recent approved review: the listing is alive and being used (mild).
        public const double RecentReview90DaysPoints = 4;
        public const double RecentReview365DaysPoints = 2;

        // Unresolved problem reports (anyone can file one, so the penalty is small and capped).
        public const double PointsPerOpenProblem = -8;
        public const double MaxProblemPenalty = -16;

        // Trust facts the site tracks.
        public const double DepositGuaranteePoints = 6;
        public const double PgpKeyPoints = 3;
        public const double SourceCodePoints = 2;

        // Completeness: a tiny tie-breaker for filled-in listings.
        public const double CompletenessPoints = 0.5; // each of: description, country, KYC policy, Tor/VPN policy

        public static ListingQuality Compute(DirectoryEntry entry, SearchSignals signals)
        {
            signals ??= SearchSignals.None;
            signals.ByEntryId.TryGetValue(entry.DirectoryEntryId, out var s);
            s ??= new SearchListingSignals();

            double status = entry.DirectoryStatus switch
            {
                DirectoryStatus.Verified => VerifiedPoints,
                DirectoryStatus.Admitted => AdmittedPoints,
                DirectoryStatus.Questionable => QuestionablePoints,
                DirectoryStatus.Scam => ScamPoints,
                _ => UnknownStatusPoints,
            };

            double mean = signals.SiteMeanRating > 0 ? signals.SiteMeanRating : DefaultMeanRating;
            double bayes = s.ReviewCount > 0
                ? ((s.ReviewCount * s.AverageRating) + (RatingPrior * mean)) / (s.ReviewCount + RatingPrior)
                : mean;
            double rating = Math.Clamp((bayes - mean) * PointsPerStarAboveMean, -MaxRatingPoints, MaxRatingPoints);

            double volume = s.ReviewCount > 0 && bayes >= MinRatingForVolume
                ? Math.Min(MaxVolumePoints, VolumePointsPerLog * Math.Log(1 + s.ReviewCount))
                : 0;

            double recency = 0;
            if (s.LastReviewUtc is DateTime last)
            {
                var age = signals.NowUtc - last;
                recency = age <= TimeSpan.FromDays(90) ? RecentReview90DaysPoints
                    : age <= TimeSpan.FromDays(365) ? RecentReview365DaysPoints : 0;
            }

            double problems = Math.Max(MaxProblemPenalty, s.OpenProblemReports * PointsPerOpenProblem);

            double trust = (s.HasDepositGuarantee ? DepositGuaranteePoints : 0)
                + (!string.IsNullOrWhiteSpace(entry.PgpKey) ? PgpKeyPoints : 0)
                + (!string.IsNullOrWhiteSpace(entry.SourceCodeLink) ? SourceCodePoints : 0);

            double completeness = CompletenessPoints * (
                (string.IsNullOrWhiteSpace(entry.Description) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(entry.CountryCode) ? 0 : 1)
                + (entry.KycPolicy is null or KycPolicy.Unknown or KycPolicy.NotStated ? 0 : 1)
                + (entry.TorPolicy != default || entry.VpnPolicy != default ? 1 : 0));

            return new ListingQuality
            {
                Status = status,
                Rating = rating,
                ReviewVolume = volume,
                Recency = recency,
                Problems = problems,
                Trust = trust,
                Completeness = completeness,
                SmoothedRating = bayes,
                ReviewCount = s.ReviewCount,
            };
        }

        public static double Band(SearchMatchTier tier) => tier switch
        {
            SearchMatchTier.NameWord => BandNameWord,
            SearchMatchTier.NameContains => BandNameContains,
            SearchMatchTier.Taxonomy => BandTaxonomy,
            SearchMatchTier.Category => BandCategory,
            SearchMatchTier.Text => BandText,
            _ => BandPartial, // exact/prefix are ordered by the protection key, not the band
        };
    }
}
