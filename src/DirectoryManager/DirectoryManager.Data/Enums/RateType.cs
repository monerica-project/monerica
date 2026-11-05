using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Which swap rates an exchange offers. Only really applies to instant swaps, so new
    /// submissions default to <see cref="NotApplicable"/>. Unknown and Not Applicable show nothing
    /// on the listing (see <see cref="RateTypeExtensions.IsShown"/>).
    /// </summary>
    public enum RateType
    {
        [Description("Unknown")]
        Unknown = 0,

        [Description("Fixed")]
        Fixed = 1,

        [Description("Float")]
        Float = 2,

        [Description("Fixed & Float")]
        FixedAndFloat = 3,

        [Description("Not Applicable")]
        NotApplicable = 4,
    }

    public static class RateTypeExtensions
    {
        /// <summary>Display order in the forms: Not Applicable first, then the real values.</summary>
        public static readonly IReadOnlyList<RateType> FormOrder = new[]
        {
            RateType.NotApplicable,
            RateType.Unknown,
            RateType.Fixed,
            RateType.Float,
            RateType.FixedAndFloat,
        };

        /// <summary>Choices on the public submission form: "Unknown" is only the stored default, never offered.</summary>
        public static readonly IReadOnlyList<RateType> PublicFormOrder = new[]
        {
            RateType.NotApplicable,
            RateType.Fixed,
            RateType.Float,
            RateType.FixedAndFloat,
        };

        /// <summary>True when the listing should show its rates (Fixed, Float or both).</summary>
        /// <returns>Whether to show it.</returns>
        public static bool IsShown(this RateType rateType) =>
            rateType is RateType.Fixed or RateType.Float or RateType.FixedAndFloat;
    }
}
