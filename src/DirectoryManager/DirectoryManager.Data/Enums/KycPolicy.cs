using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// A listing's Know-Your-Customer stance. The property that uses this is nullable;
    /// a null column value means "Not Stated" (no policy recorded). <see cref="NotStated"/>
    /// exists so "Not Stated" can be a first-class filter/display value; it is not stored
    /// (null represents it in the database). 0 is reserved as <see cref="Unknown"/> and is
    /// never a real policy. The <see cref="DescriptionAttribute"/> carries the human-readable
    /// label so the full wording renders without a lookup table.
    /// </summary>
    public enum KycPolicy
    {
        Unknown = 0,

        [Description("Not Stated")]
        NotStated = 1,

        [Description("Guaranteed No KYC")]
        GuaranteedNoKyc = 2,

        [Description("Rare KYC")]
        RareKyc = 3,

        [Description("Shotgun KYC")]
        ShotgunKyc = 4,

        [Description("Mandatory KYC")]
        MandatoryKyc = 5,

        [Description("Varies By Provider")]
        VariesByProvider = 6,
    }
}
