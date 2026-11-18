using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Whether the service can be used over Tor. <see cref="Unknown"/> (0) is the default and is
    /// shown in forms as "Not Stated"; it renders nothing on the listing page. A listing with a
    /// .onion link works over Tor, so <see cref="NotAllowed"/> is rejected for it (and never shown).
    /// </summary>
    public enum TorPolicy
    {
        [Description("Not Stated")]
        Unknown = 0,

        [Description("Allowed")]
        Allowed = 1,

        [Description("Not Allowed")]
        NotAllowed = 2,

        /// <summary>Works, but a Tor connection may get the account held for extra checks.</summary>
        [Description("May Trigger Verification")]
        MayTriggerVerification = 3,
    }
}
