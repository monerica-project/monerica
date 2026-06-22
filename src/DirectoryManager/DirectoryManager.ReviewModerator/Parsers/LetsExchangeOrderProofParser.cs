using System.Text.Json;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// LetsExchange's order page (https://letsexchange.io/?transactionId={ID}, also surfaced via
    /// the /transaction-status route and the my.letsexchange.io account UI) is a CLIENT-ONLY
    /// Nuxt 3 SPA. The served HTML is just the marketing shell with the swap widget in a
    /// skeleton-loader state; the only trace of the order is the transactionId sitting in the
    /// route path inside window.__NUXT__ / __NUXT_DATA__. After hydration the SPA fetches the
    /// order from LetsExchange's API (api.letsexchange.io) and renders it.
    ///
    /// Therefore scraping the HTML is useless — this parser must hit the JSON endpoint.
    ///
    /// NEEDED FROM YOU (the two TODOs below):
    ///   1. The API URL the SPA requests for a given transactionId. Open
    ///      https://letsexchange.io/?transactionId={ID} (or /transaction-status) with devtools
    ///      Network tab → Fetch/XHR and copy the request that returns the order JSON. It is
    ///      almost certainly an api.letsexchange.io route (their public API is documented at
    ///      https://api.letsexchange.io/doc) and may need the transaction_id in the path/query
    ///      or POST body.
    ///   2. The JSON response shape for a COMPLETED order — exact field names for:
    ///        status, deposit (sent) asset+amount, withdrawal (received) asset+amount,
    ///        payout/recipient address, provider order id, finished timestamp — plus the literal
    ///        value the status field holds when finished (e.g. "success" / "finished" /
    ///        "completed", or a numeric code), and the values for the non-terminal/failure states
    ///        so MapStatus can distinguish in-progress (retry) from refunded/expired (reject).
    ///
    /// Once you paste those, this becomes a ~15-line concrete parser. Until then it returns
    /// Unknown so the worker flags LetsExchange reviews for a human rather than guessing.
    /// </summary>
    public sealed class LetsExchangeOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "letsexchange.io", "my.letsexchange.io" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            // ID arrives as ?transactionId={id}; fall back to a trailing path segment in case
            // account-side links use /transaction-status/{id} or similar.
            var id = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("transactionId");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = uri.Segments.LastOrDefault()?.Trim('/');
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            // TODO(LetsExchange API #1): return the real JSON endpoint for this id, e.g. something like:
            //   return new Uri($"https://api.letsexchange.io/api/v1/transactions/{Uri.EscapeDataString(id)}");
            // (confirm the exact path/host from the devtools capture). Returning null for now =>
            // worker flags LetsExchange reviews (no false approvals).
            return null;
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("letsexchange: expected JSON from order API");
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                _ = doc.RootElement;

                // TODO(LetsExchange API #2): map the real JSON fields. Sketch (adjust names):
                //   var statusRaw = root.GetProperty("status").GetString();
                //   var status = MapStatus(statusRaw);   // "success"/"finished" => Completed
                //   return new OrderProofResult {
                //       Status           = status,
                //       SentAsset        = root.GetProperty("coin_from").GetString()?.ToUpperInvariant(),
                //       SentAmount       = decimal.Parse(root.GetProperty("deposit_amount").GetString()!),
                //       ReceivedAsset    = root.GetProperty("coin_to").GetString()?.ToUpperInvariant(),
                //       ReceivedAmount   = decimal.Parse(root.GetProperty("withdrawal_amount").GetString()!),
                //       RecipientAddress = root.GetProperty("withdrawal").GetString(),
                //       ProviderOrderId  = root.GetProperty("transaction_id").GetString(),
                //       Note             = $"letsexchange:{statusRaw}",
                //   };
                return OrderProofResult.Unknown("letsexchange: JSON mapping not configured yet");
            }
            catch (JsonException ex)
            {
                return OrderProofResult.Unknown($"letsexchange: bad JSON ({ex.Message})");
            }
        }
    }
}