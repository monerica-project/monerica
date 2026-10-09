namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Single source of truth for which <see cref="Liquidity"/> values are shown/offered where.
    /// Keeps the "Unknown and NotApplicable render nothing" rule out of the views.
    /// </summary>
    public static class LiquidityExtensions
    {
        /// <summary>True when this value should be displayed publicly (everything except
        /// <see cref="Liquidity.Unknown"/> and <see cref="Liquidity.NotApplicable"/>).</summary>
        public static bool IsPubliclyShown(this Liquidity liquidity) =>
            liquidity != Liquidity.Unknown && liquidity != Liquidity.NotApplicable;

        /// <summary>Values offered in the submission/edit dropdowns: NotApplicable (the default)
        /// plus the real sources. <see cref="Liquidity.Unknown"/> is never user-selectable.</summary>
        public static readonly IReadOnlyList<Liquidity> Selectable = new[]
        {
            Liquidity.NotApplicable,
            Liquidity.Own,
            Liquidity.Mixed,
            Liquidity.OwnAndThirdParty,
            Liquidity.ThirdParty,
            Liquidity.VariesByProvider,
        };

        /// <summary>Values offered as filter checkboxes — only the meaningful sources.</summary>
        public static readonly IReadOnlyList<Liquidity> Filterable = new[]
        {
            Liquidity.Own,
            Liquidity.Mixed,
            Liquidity.OwnAndThirdParty,
            Liquidity.ThirdParty,
            Liquidity.VariesByProvider,
        };
    }
}
