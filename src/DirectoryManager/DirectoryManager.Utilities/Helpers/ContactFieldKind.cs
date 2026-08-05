namespace DirectoryManager.Utilities.Helpers
{
    /// <summary>
    /// Which listing contact field is being rendered. Drives how a bare handle (no URL,
    /// no platform named) is linkified: the site's submission form tells users to enter
    /// "@handle on X" for Social and "@handle on Telegram" for Messenger, so a bare handle
    /// defaults to X or Telegram respectively.
    /// </summary>
    public enum ContactFieldKind
    {
        /// <summary>No platform assumptions (legacy behaviour).</summary>
        Generic = 0,

        /// <summary>Email field — values are addresses, rendered as obfuscated mailto links.</summary>
        Email = 1,

        /// <summary>Messenger field — a bare handle defaults to Telegram.</summary>
        Messenger = 2,

        /// <summary>Social field — a bare handle defaults to X (Twitter).</summary>
        Social = 3,
    }
}
