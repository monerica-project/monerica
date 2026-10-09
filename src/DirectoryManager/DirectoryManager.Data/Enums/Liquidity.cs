using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Where a listing's swap liquidity comes from. Primarily relevant to instant-swap exchanges.
    /// <para>
    /// <see cref="Unknown"/> (0) is the reserved default for rows that predate this field.
    /// <see cref="NotApplicable"/> is the form default and what most listings are set to. Only
    /// <see cref="Own"/> / <see cref="Mixed"/> / <see cref="OwnAndThirdParty"/> / <see cref="ThirdParty"/> /
    /// <see cref="VariesByProvider"/> are shown publicly — Unknown and NotApplicable render nothing on the listing page.
    /// </para>
    /// </summary>
    public enum Liquidity
    {
        Unknown = 0,

        [Description("Not Applicable")]
        NotApplicable = 1,

        [Description("Own")]
        Own = 2,

        [Description("Mixed")]
        Mixed = 3,

        [Description("Third Party")]
        ThirdParty = 4,

        [Description("Varies By Provider")]
        VariesByProvider = 5,

        [Description("Own & Third-Party")]
        OwnAndThirdParty = 6,
    }
}
