namespace DirectoryManager.Common.Constants
{
    public class IntegerConstants
    {
        public const int MaxMainSponsoredListings = 5;

        // Extra subcategories a listing can also appear in, besides its main one.
        public const int MaxAdditionalSubcategories = 3;

        public const int MinRequiredSubcategories = 2;
        public const int MaxSubcategorySponsoredListings = 1;

        public const int MinRequiredCategories = 2;
        public const int MaxCategorySponsoredListings = 1;

        public const int MaxMainSponsorsPerSubcategory = 5;

        /// <summary>A listing must be Verified AND listed in the directory at least this many
        /// days before it is eligible to become a sponsor.</summary>
        public const int MinimumDaysListedBeforeSponsoring = 180;

        // A sponsored video is delivered within this many days of payment.
        public const int VideoSponsorDeliveryDays = 30;

        // Approximate length of a video that carries a 1-minute sponsor segment.
        public const int VideoSponsorSegmentVideoMinutes = 10;

        // A product demo that costs more than this (USD) is paid for by the sponsor, in full.
        public const int VideoSponsorDemoCostThresholdUsd = 100;

        // Sanity ceiling on a buyer-entered demo cost (USD).
        public const int VideoSponsorDemoCostMaxUsd = 100000;

        // Cross-tier loyalty discount: a listing that already holds a paid sponsorship of one tier
        // gets this percent off ADDITIONAL placements of a DIFFERENT tier (never a same-tier renewal).
        // When a listing holds more than one tier, the highest discount wins.
        // (Config here for now; can be surfaced as an admin setting later.)
        public const int MainSponsorCrossTierDiscountPercent = 20;
        public const int CategorySponsorCrossTierDiscountPercent = 15;
        public const int SubcategorySponsorCrossTierDiscountPercent = 10;
    }
}