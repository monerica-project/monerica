using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// CypherGoat (https://cyphergoat.com) is a no-KYC swap AGGREGATOR. Its transaction page
    /// (https://cyphergoat.com/transaction/{GUID}) is server-rendered (Go + templ + htmx), so the
    /// completed-order data is present directly in the served HTML — no JSON API needed.
    ///
    /// Confirmed from a completed sample:
    ///   - "Transaction Status" cell holds "Completed" on a finished swap (plus a "Transaction
    ///     Complete" success block). Only "Completed" is confirmed terminal-success; CypherGoat's
    ///     in-progress / failure wording is not yet known, so any other status maps to Unknown
    ///     (=> worker retries then flags, never auto-approves/rejects on a guess).
    ///   - "You Send" / "You Receive" expose AMOUNTS ONLY. Because CypherGoat is an aggregator it
    ///     renders the asset as a coin icon, not ticker text, so SentAsset/ReceivedAsset cannot be
    ///     parsed here and are left null. Completion still approves (+ valid-order); the USD
    ///     money-band is simply skipped (it needs the sent ticker) — the designed best-effort path.
    ///   - "Send Address" is the payout/recipient address (used for cross-checks).
    ///   - The header shows the underlying routed exchange (e.g. "bitxchange") and "Exchange Link"
    ///     points at that provider's own order; both are recorded in Note for the audit trail.
    ///   - "CypherGoat ID" is the platform order id (the {GUID} in the URL) => ProviderOrderId.
    ///   - No completion timestamp is rendered, so CompletedAtUtc is left null.
    /// </summary>
    public sealed class CypherGoatOrderProofParser : IOrderProofParser
    {
        private const string CompletedStatus = "Completed";

        private static readonly Regex StatusRx = new (
            "Transaction Status</div>\\s*<div class=\"section-value\">\\s*<span[^>]*>([^<]+)</span>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SendAmountRx = new (
            "You Send</div>\\s*<div class=\"section-value\">\\s*<span[^>]*>\\s*([0-9][0-9.,]*)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ReceiveAmountRx = new (
            "You Receive</div>\\s*<div class=\"section-value\">\\s*<span[^>]*>\\s*([0-9][0-9.,]*)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex RecipientRx = new (
            "Send Address</div>\\s*<div class=\"code-block\">\\s*([^<\\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ExchangeLinkRx = new (
            "Exchange Link</div>\\s*<div class=\"section-value\">\\s*<a href=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex RoutedViaRx = new (
            "<h2 class=\"transaction-header\">\\s*([^<]+?)\\s*</h2>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex ProviderIdRx = new (
            "CypherGoat ID</div>\\s*<div class=\"section-value[^\"]*\">\\s*([0-9a-fA-F-]{36})",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex TransactionPathRx = new (
            "/transaction/([^/?#]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IReadOnlyCollection<string> Hosts { get; } = new[] { "cyphergoat.com" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            var match = TransactionPathRx.Match(uri.AbsolutePath);
            if (!match.Success)
            {
                return null;
            }

            var id = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            // The submitted page IS the authoritative completion view; normalize to the canonical
            // clearnet host + /transaction/{id} path.
            return new Uri($"https://cyphergoat.com/transaction/{Uri.EscapeDataString(id)}");
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("cyphergoat: empty response body");
            }

            var statusMatch = StatusRx.Match(responseBody);
            if (!statusMatch.Success)
            {
                return OrderProofResult.Unknown("cyphergoat: transaction status not found in page");
            }

            var rawStatus = statusMatch.Groups[1].Value.Trim();
            var status = string.Equals(rawStatus, CompletedStatus, StringComparison.OrdinalIgnoreCase)
                ? OrderProofStatus.Completed
                : OrderProofStatus.Unknown;

            var sentAmount = ParseAmount(SendAmountRx.Match(responseBody));
            var receivedAmount = ParseAmount(ReceiveAmountRx.Match(responseBody));

            var recipient = Group(RecipientRx, responseBody);
            var providerOrderId = Group(ProviderIdRx, responseBody);
            var routedVia = Group(RoutedViaRx, responseBody);
            var exchangeLink = Group(ExchangeLinkRx, responseBody);

            var note = $"cyphergoat:{rawStatus}"
                + (string.IsNullOrEmpty(routedVia) ? string.Empty : $" via {routedVia}")
                + (string.IsNullOrEmpty(exchangeLink) ? string.Empty : $" ({exchangeLink})")
                + "; assets not exposed by aggregator page";

            return new OrderProofResult
            {
                Status = status,

                // Aggregator page renders the coin as an icon, not text — no tickers available.
                SentAsset = null,
                SentAmount = sentAmount,
                ReceivedAsset = null,
                ReceivedAmount = receivedAmount,

                RecipientAddress = recipient,
                ProviderOrderId = providerOrderId,
                CompletedAtUtc = null,
                Note = note,
            };
        }

        private static string? Group(Regex rx, string body)
        {
            var m = rx.Match(body);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static decimal? ParseAmount(Match match)
        {
            if (!match.Success)
            {
                return null;
            }

            var raw = match.Groups[1].Value.Trim();
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }
    }
}