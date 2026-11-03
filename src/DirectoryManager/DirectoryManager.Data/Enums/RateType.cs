using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Which swap rates an exchange offers. <see cref="Unknown"/> (0) is the default and shows
    /// nothing on the listing; it's mainly filled in for instant-swap exchanges.
    /// </summary>
    public enum RateType
    {
        [Description("Unknown")]
        Unknown = 0,

        [Description("Fixed")]
        Fixed = 1,

        [Description("Float")]
        Float = 2,

        [Description("Fixed & float")]
        FixedAndFloat = 3,
    }
}
