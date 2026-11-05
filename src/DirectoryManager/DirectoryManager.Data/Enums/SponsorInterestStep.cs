using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>How far someone got when looking at sponsoring a listing (furthest step wins).</summary>
    public enum SponsorInterestStep
    {
        [Description("Unknown")]
        Unknown = 0,

        [Description("Viewed options")]
        ViewedOptions = 1,

        [Description("Viewed prices")]
        ViewedPrices = 2,

        [Description("Picked a duration")]
        PickedDuration = 3,

        [Description("Started payment")]
        StartedPayment = 4,

        // Video sponsorship checkout (kept apart from the site-sponsor steps above; see IsVideo).

        [Description("Video: viewed prices")]
        VideoViewedPrices = 10,

        [Description("Video: picking videos")]
        VideoPickedVideos = 11,

        [Description("Video: reviewing order")]
        VideoReviewedOrder = 12,

        [Description("Video: started payment")]
        VideoStartedPayment = 13,
    }

    public static class SponsorInterestStepExtensions
    {
        /// <summary>True for the video sponsorship checkout steps.</summary>
        public static bool IsVideo(this SponsorInterestStep step) => (int)step >= (int)SponsorInterestStep.VideoViewedPrices;

        /// <summary>True when the visitor got as far as creating a payment (site or video).</summary>
        public static bool IsStartedPayment(this SponsorInterestStep step) =>
            step is SponsorInterestStep.StartedPayment or SponsorInterestStep.VideoStartedPayment;
    }
}
