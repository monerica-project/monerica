using System.Globalization;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// Changee renders order details server-side, so a plain GET is enough.
    ///
    /// The completed/finished state only exists on the step-4 page, so regardless of
    /// which step the reviewer pasted (step-2 payment, step-3, step-4) we always look up
    ///   https://changee.com/exchange/step-4?id={EXCHANGE_ID}
    /// — the authoritative completion view. If the order isn't actually finished, step-4
    /// shows an in-progress state (or redirects to the live step, which the fetcher follows),
    /// and we map it to a non-terminal status => the worker flags/retries, never approves.
    ///
    /// Verified against real samples (completed id=ntq6a35e22569841, in-progress id=hrz6a38cfd2394d4):
    ///   COMPLETED markers:  body text "Exchange completed", header "Successfully", and the
    ///                       step-4 icon carries class "success" (class="step-icon step-4 success").
    ///   IN-PROGRESS:        step-3 icon is "active" / step-4 has no "success".
    ///   Fields:             "You send" 0.02384 BTC; "You receive" #expected_amount_to 4.762 + XMR;
    ///                       "Recipient's address" (completed) / "Your destination address" (payment);
    ///                       "Date" 20.06.2026 00:43:17 => completion timestamp.
    /// </summary>
    public sealed class ChangeeOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "changee.com" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            var id = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return null; // no id => unusable => worker flags
            }

            // Always resolve to the completion view; it's the only page that shows finished state.
            return new Uri($"https://changee.com/exchange/step-4?id={Uri.EscapeDataString(id)}");
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("empty body");
            }

            var (sentAmt, sentAsset) = ExtractLabeledAmount(responseBody, "You send");
            var (recvAmt, recvAsset) = ExtractReceiveAmount(responseBody);
            var recipient = ExtractRecipientAddress(responseBody);
            var status = DetectStatus(responseBody);
            var completedAt = status == OrderProofStatus.Completed ? ExtractCompletionDateUtc(responseBody) : null;

            return new OrderProofResult
            {
                Status = status,
                SentAsset = sentAsset,
                SentAmount = sentAmt,
                ReceivedAsset = recvAsset,
                ReceivedAmount = recvAmt,
                RecipientAddress = recipient,
                CompletedAtUtc = completedAt,
                Note = "changee",
            };
        }

        private static OrderProofStatus DetectStatus(string html)
        {
            // Terminal success: body says "Exchange completed", or the step-4 icon is marked done.
            if (Regex.IsMatch(html, @"Exchange\s+completed", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(html, @"step-4[^""]*success", RegexOptions.IgnoreCase))
            {
                return OrderProofStatus.Completed;
            }

            // Non-terminal: map the active step.
            if (Regex.IsMatch(html, @"step-3[^""]*\bactive\b", RegexOptions.IgnoreCase))
            {
                return OrderProofStatus.AwaitingDeposit; // Payment step active => not finished
            }

            if (Regex.IsMatch(html, @"step-4[^""]*\bactive\b", RegexOptions.IgnoreCase))
            {
                return OrderProofStatus.Exchanging;
            }

            return OrderProofStatus.Unknown;
        }

        // "<label>You send</label><div>0.02384 BTC</div>"
        private static (decimal? amount, string? asset) ExtractLabeledAmount(string html, string label)
        {
            var m = Regex.Match(
                html,
                $@"<label>\s*{Regex.Escape(label)}\s*</label>\s*<div>\s*~?\s*([\d.,]+)\s*([A-Za-z0-9]+)\s*</div>",
                RegexOptions.IgnoreCase);
            return m.Success ? (ParseDecimal(m.Groups[1].Value), m.Groups[2].Value.ToUpperInvariant()) : (null, null);
        }

        // amount in <span id="expected_amount_to">4.762</span>, asset as text after the span ("XMR")
        private static (decimal? amount, string? asset) ExtractReceiveAmount(string html)
        {
            var amtM = Regex.Match(html, @"id=""expected_amount_to""[^>]*>\s*([\d.,]+)\s*<", RegexOptions.IgnoreCase);
            var assetM = Regex.Match(html, @"id=""expected_amount_to"".*?</span>\s*([A-Za-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            decimal? amt = amtM.Success ? ParseDecimal(amtM.Groups[1].Value) : null;
            string? asset = assetM.Success ? assetM.Groups[1].Value.ToUpperInvariant() : null;
            return (amt, asset);
        }

        // "Recipient's address" (completed page) or "Your destination address" (payment page).
        private static string? ExtractRecipientAddress(string html)
        {
            var m = Regex.Match(
                html,
                @"(?:Recipient(?:'|&#39;|&#x27;)?s\s+address|Your\s+destination\s+address):?\s*</label>\s*<div[^>]*>\s*([^<\s]+)",
                RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        // "<label>Date</label><div ...>20.06.2026 00:43:17</div>" => dd.MM.yyyy HH:mm:ss (assumed UTC)
        private static DateTime? ExtractCompletionDateUtc(string html)
        {
            var m = Regex.Match(
                html,
                @"<label>\s*Date\s*</label>\s*<div[^>]*>\s*(\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}:\d{2})",
                RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    m.Groups[1].Value,
                    "dd.MM.yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            return null;
        }

        private static decimal? ParseDecimal(string raw)
        {
            raw = raw.Replace(",", string.Empty);
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
    }
}