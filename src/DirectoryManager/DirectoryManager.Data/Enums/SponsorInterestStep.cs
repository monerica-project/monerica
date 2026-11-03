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
    }
}
