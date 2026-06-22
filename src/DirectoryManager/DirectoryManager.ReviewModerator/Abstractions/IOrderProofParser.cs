using DirectoryManager.Data.Enums;

namespace DirectoryManager.ReviewModerator.Abstractions
{
    /// <summary>
    /// Per-domain strategy that turns a review's order URL (plus any supporting context)
    /// into a normalized <see cref="OrderProofResult"/>. One implementation per supported
    /// exchange domain. Adding a new exchange = add one class + register it.
    /// </summary>
    public interface IOrderProofParser
    {
        /// <summary>
        /// Host(s) this parser handles, lower-cased, no scheme, no "www."
        /// (e.g. "changee.com", "ghostswap.io"). Matched against the order URL host.
        /// </summary>
        IReadOnlyCollection<string> Hosts { get; }

        /// <summary>
        /// True if this exchange's order details are only readable after supplying
        /// supporting content (e.g. a receiving wallet address). When true and the
        /// review has no <c>OrderProofContext</c>, the worker flags instead of fetching.
        /// </summary>
        bool RequiresUnlockContext { get; }

        /// <summary>
        /// Build the canonical lookup target (the HTML page or JSON API endpoint) for a
        /// given submitted order URL. Returns null if the submitted URL is unusable
        /// (wrong shape / missing id), which the worker treats as a flag.
        /// </summary>
        /// <param name="submittedOrderUrl">The OrderUrl exactly as the reviewer submitted it.</param>
        /// <param name="orderProofContext">Optional supporting content (e.g. wallet address).</param>
        Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext);

        /// <summary>
        /// Parse a fetched body (HTML or JSON, depending on the domain) into a result.
        /// Implementations must be conservative: if the terminal-success signal is not
        /// unambiguous, return <see cref="OrderProofStatus.Unknown"/> so the worker flags
        /// rather than approves.
        /// </summary>
        OrderProofResult Parse(string responseBody, string contentType);
    }

    /// <summary>
    /// Normalized, domain-agnostic view of a swap order, produced by an
    /// <see cref="IOrderProofParser"/> and consumed by the moderation decision engine.
    /// </summary>
    public sealed class OrderProofResult
    {
        public OrderProofStatus Status { get; init; } = OrderProofStatus.Unknown;

        // Deposit (what the user sent in).
        public string? SentAsset { get; init; }
        public decimal? SentAmount { get; init; }

        // Payout (what the user received).
        public string? ReceivedAsset { get; init; }
        public decimal? ReceivedAmount { get; init; }

        /// <summary>The address the payout was sent to, if the proof exposes it. Used for cross-checks.</summary>
        public string? RecipientAddress { get; init; }

        /// <summary>The exchange's own order id, if the proof echoes it back. Used for replay/dedupe.</summary>
        public string? ProviderOrderId { get; init; }

        /// <summary>Completion timestamp if available, for pricing the swap at order time.</summary>
        public DateTime? CompletedAtUtc { get; init; }

        /// <summary>Free-text note from the parser for the audit trail (raw status string, anomalies, etc.).</summary>
        public string? Note { get; init; }

        public bool IsTerminalSuccess => this.Status == OrderProofStatus.Completed;

        public bool IsTerminalFailure =>
            this.Status is OrderProofStatus.Failed
                or OrderProofStatus.Expired
                or OrderProofStatus.Refunded
                or OrderProofStatus.Cancelled;

        public static OrderProofResult Unknown(string? note = null) =>
            new () { Status = OrderProofStatus.Unknown, Note = note };
    }
}
