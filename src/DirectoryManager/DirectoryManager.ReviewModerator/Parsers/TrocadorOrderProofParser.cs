using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// Trocador renders the checkout/status page server-side, so a plain GET is enough.
    /// The trade id is a PATH segment, not a query param:
    ///   https://trocador.app/en/checkout/{TRADE_ID}
    /// We normalize any language prefix to /en/ so the English status markers below match.
    ///
    /// Verified against a real completed sample (id=AzsEy2JTqH, provider Swapuz):
    ///   COMPLETED markers:  "Transaction successfully completed" (status block + done.png),
    ///                       and the details table "Status: Finished".
    ///   Send:               input name="amount_from" + the coin icon (icons/xmr.svg => XMR).
    ///   Receive:            input name="amount_to"   + the coin icon (icons/pol.svg => POL).
    ///   Recipient address:  #address_user   (the user's receiving address).
    ///   Deposit address:    #address_provider (where the user sent funds).
    /// No full completion date is shown (only "Created at HH:MM UTC"), so CompletedAtUtc is
    /// left null; valuation falls back to spot once a price source exists.
    ///
    /// Trade access is by the unguessable id in the URL (a bearer capability), so no unlock
    /// context is needed. If Trocador ever serves a login wall / hides data for an anonymous
    /// fetch, Parse falls through to Unknown => the worker flags rather than approves.
    /// </summary>
    public sealed class TrocadorOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "trocador.app" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Path is /{lang}/checkout/{id} or /checkout/{id}; grab the segment after "checkout".
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            var idx = Array.FindIndex(segments, s => s.Equals("checkout", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= segments.Length)
            {
                return null; // not a checkout URL / no id => worker flags
            }

            var id = segments[idx + 1];
            return new Uri($"https://trocador.app/en/checkout/{Uri.EscapeDataString(id)}");
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("empty body");
            }

            var status = DetectStatus(responseBody);

            return new OrderProofResult
            {
                Status = status,
                SentAsset = TickerAfter(responseBody, "amount_from"),
                SentAmount = AmountOf(responseBody, "amount_from"),
                ReceivedAsset = TickerAfter(responseBody, "amount_to"),
                ReceivedAmount = AmountOf(responseBody, "amount_to"),
                RecipientAddress = AddressOf(responseBody, "address_user"),
                Note = "trocador",
            };
        }

        private static OrderProofStatus DetectStatus(string html)
        {
            var cell = Regex.Match(html, @"Status:\s*</th>\s*<td[^>]*>\s*([A-Za-z]+)", RegexOptions.IgnoreCase);
            var word = cell.Success ? cell.Groups[1].Value.ToLowerInvariant() : null;

            if (Regex.IsMatch(html, @"successfully\s+completed", RegexOptions.IgnoreCase) || word == "finished")
            {
                return OrderProofStatus.Completed;
            }

            return word switch
            {
                "expired" => OrderProofStatus.Expired,
                "refunded" => OrderProofStatus.Refunded,
                "halted" or "failed" => OrderProofStatus.Failed,
                "cancelled" or "canceled" => OrderProofStatus.Cancelled,
                "waiting" or "new" or "pending" => OrderProofStatus.AwaitingDeposit,
                "confirming" or "confirmation" => OrderProofStatus.Confirming,
                "exchanging" or "trading" => OrderProofStatus.Exchanging,
                "sending" => OrderProofStatus.Sending,
                _ => OrderProofStatus.Unknown,
            };
        }

        // <input ... name="amount_from" value="11.18296" ...>
        private static decimal? AmountOf(string html, string field)
        {
            var m = Regex.Match(html, $@"name=""{field}""[^>]*?value=""([\d.,]+)""", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            var raw = m.Groups[1].Value.Replace(",", string.Empty);
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        // first coin icon after the input: .../static/img/icons/xmr.svg => "XMR"
        private static string? TickerAfter(string html, string field)
        {
            var m = Regex.Match(html, $@"name=""{field}"".*?icons/([A-Za-z0-9]+)\.svg", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }

        // <div id="address_user" ...>0x1f45...</div>
        private static string? AddressOf(string html, string elementId)
        {
            var m = Regex.Match(html, $@"id=""{elementId}""[^>]*>\s*([^<\s]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}