namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Normalized swap-order state, mapped from each exchange's own status vocabulary
    /// by the per-domain <c>IOrderProofParser</c>. The auto-moderator only ever approves
    /// on a terminal-success state; everything else is retried or flagged.
    /// </summary>
    public enum OrderProofStatus
    {
        /// <summary>Could not determine the status from the proof page/API.</summary>
        Unknown = 0,

        // ---- Non-terminal (still in progress) ----
        AwaitingDeposit = 1,
        Confirming = 2,
        Exchanging = 3,
        Sending = 4,

        // ---- Terminal success ----
        Completed = 10,

        // ---- Terminal failure ----
        Failed = 20,
        Expired = 21,
        Refunded = 22,
        Cancelled = 23,

        /// <summary>
        /// The page exists but order details are locked behind supporting content
        /// (e.g. the receiving wallet address) that was not supplied on the review.
        /// </summary>
        NeedsUnlockContext = 30,
    }
}
