using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// BitcoinVN renders the order details server-side and, helpfully, exposes a machine-readable
    /// status on the order card: data-status="completed". A plain GET is enough.
    /// The trade id is a PATH segment: https://bitcoinvn.io/orders/{ID} (localized variants are
    /// /{lang}/orders/{ID}); we normalize to the English /orders/{ID} so the markers below match.
    ///
    /// Verified against a real completed sample (id BVYMK7DF):
    ///   status:      data-status="completed" (also a text-bg-success "Completed" badge).
    ///   deposit/settle: data-deposit="xmr" (what the user SENT), data-settle="btc" (RECEIVED).
    ///   "You've sent"     leg => 5 XMR;  "You've received" leg => 0.02385068 BTC.
    ///   recipient:   the on-page address is truncated, but the full one is in the "Swap again"
    ///                link's order[settleData][address]= query param.
    /// No reliable machine timestamp (the footer time is formatted in the viewer's tz), so
    /// CompletedAtUtc is left null; valuation falls back to spot once a price source exists.
    ///
    /// Only "completed" is confirmed from a real sample; the other status strings are inferred
    /// from common conventions. Unrecognized statuses map to Unknown => the worker flags rather
    /// than guessing. Adjust the switch if a real failed/expired sample shows a different word.
    ///
    /// Orders are addressable by their id (a bearer capability), so we fetch anonymously. If an
    /// anonymous request returns a login page instead of the order, no data-status is found =>
    /// Unknown => flag (never a false approve).
    /// </summary>
    public sealed class BitcoinVnOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "bitcoinvn.io" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Path is /orders/{id} or /{lang}/orders/{id}; grab the segment after "orders".
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.FindIndex(segments, s => s.Equals("orders", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= segments.Length)
            {
                return null; // /orders (history) or no id => worker flags
            }

            var id = segments[idx + 1];
            return new Uri($"https://bitcoinvn.io/orders/{Uri.EscapeDataString(id)}");
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("empty body");
            }

            var statusRaw = Match(responseBody, @"data-status=""([^""]+)""");
            if (statusRaw is null)
            {
                return OrderProofResult.Unknown("no order card / status");
            }

            var (sentAmt, sentLegAsset) = LegAmount(responseBody, "deposit");
            var (recvAmt, recvLegAsset) = LegAmount(responseBody, "settle");

            return new OrderProofResult
            {
                Status = MapStatus(statusRaw),
                SentAsset = AssetAttr(responseBody, "deposit") ?? sentLegAsset,
                SentAmount = sentAmt,
                ReceivedAsset = AssetAttr(responseBody, "settle") ?? recvLegAsset,
                ReceivedAmount = recvAmt,
                RecipientAddress = RecipientAddress(responseBody),
                Note = "bitcoinvn",
            };
        }

        private static OrderProofStatus MapStatus(string raw) => raw.Trim().ToLowerInvariant() switch
        {
            "completed" or "settled" or "finished" or "complete" => OrderProofStatus.Completed,
            "pending" or "awaiting" or "new" or "unpaid" or "open" => OrderProofStatus.AwaitingDeposit,
            "confirming" or "processing" or "received" or "detected" => OrderProofStatus.Confirming,
            "exchanging" or "trading" => OrderProofStatus.Exchanging,
            "sending" or "settling" or "payout" => OrderProofStatus.Sending,
            "expired" or "timeout" => OrderProofStatus.Expired,
            "refunded" or "returned" => OrderProofStatus.Refunded,
            "failed" or "cancelled" or "canceled" or "error" => OrderProofStatus.Failed,
            _ => OrderProofStatus.Unknown,
        };

        // data-deposit="xmr" / data-settle="btc"
        private static string? AssetAttr(string html, string which)
        {
            var v = Match(html, $@"data-{which}=""([A-Za-z0-9]+)""");
            return string.IsNullOrWhiteSpace(v) ? null : v!.ToUpperInvariant();
        }

        // Scope to the "deposit" or "settle" leg, then read "5&nbsp;XMR" / "0.02385068&nbsp;BTC".
        private static (decimal? amount, string? asset) LegAmount(string html, string legClass)
        {
            var seg = legClass == "deposit"
                ? Match(html, @"order-summary-leg-deposit(.*?)order-summary-leg-settle", RegexOptions.Singleline)
                : Match(html, @"order-summary-leg-settle(.*)$", RegexOptions.Singleline);

            if (seg is null)
            {
                return (null, null);
            }

            var m = Regex.Match(
                seg,
                @"class=""asset[^""]*"">\s*([\d.,]+)\s*(?:&nbsp;|&#160;|&#xA0;|\s)\s*([A-Za-z0-9]+)",
                RegexOptions.IgnoreCase);

            if (!m.Success)
            {
                return (null, null);
            }

            return (ParseDecimal(m.Groups[1].Value), m.Groups[2].Value.ToUpperInvariant());
        }

        // Full recipient address from the "Swap again" link: order[settleData][address]=...
        private static string? RecipientAddress(string html)
        {
            var v = Match(html, @"order%5BsettleData%5D%5Baddress%5D=([^&""]+)");
            return string.IsNullOrWhiteSpace(v) ? null : Uri.UnescapeDataString(v!);
        }

        private static string? Match(string input, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
        {
            var m = Regex.Match(input, pattern, options);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static decimal? ParseDecimal(string raw)
        {
            raw = raw.Replace(",", string.Empty);
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }
}