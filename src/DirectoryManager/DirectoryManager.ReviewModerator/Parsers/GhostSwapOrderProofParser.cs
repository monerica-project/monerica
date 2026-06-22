using System.Text.Json;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// GhostSwap's transaction page (https://ghostswap.io/txs/?id={EXCHANGE_ID}) is rendered
    /// CLIENT-SIDE: the served HTML shows only "unknown"/"Loading..." placeholders, and the
    /// real values (status, sent/received assets+amounts, recipient address) are filled in by
    /// /wp-content/themes/toka/exchanger/main.js, which calls a JSON API keyed by the id.
    ///
    /// Therefore scraping the HTML is useless — this parser must hit GhostSwap's JSON endpoint.
    ///
    /// NEEDED FROM YOU (the two TODOs below):
    ///   1. The API URL pattern main.js requests for a given id
    ///      (open the page with devtools Network tab; e.g. it may be something like
    ///       https://ghostswap.io/wp-json/.../order?id={id}  or an api.ghostswap.io route).
    ///   2. The JSON response shape for a COMPLETED order — exact field names for:
    ///        status, from-asset, from-amount, to-asset, to-amount, payout/recipient address,
    ///        provider order id, finished timestamp — plus the literal value the "status"
    ///        field holds when finished (e.g. "finished" / "completed" / "success").
    ///
    /// Once you paste those, this becomes a ~15-line concrete parser. Until then it returns
    /// Unknown so the worker flags GhostSwap reviews for a human rather than guessing.
    /// </summary>
    public sealed class GhostSwapOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "ghostswap.io" };

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
                return null;
            }

            // TODO(GhostSwap API #1): return the real JSON endpoint for this id, e.g.:
            //   return new Uri($"https://ghostswap.io/wp-json/ghostswap/v1/order?id={Uri.EscapeDataString(id)}");
            // Returning null for now => worker flags GhostSwap reviews (no false approvals).
            return null;
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("ghostswap: expected JSON from order API");
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                _ = doc.RootElement;

                // TODO(GhostSwap API #2): map the real JSON fields. Sketch (adjust names):
                //   var statusRaw = root.GetProperty("status").GetString();
                //   var status = MapStatus(statusRaw);   // "finished"/"completed" => Completed
                //   return new OrderProofResult {
                //       Status          = status,
                //       SentAsset       = root.GetProperty("from_currency").GetString()?.ToUpperInvariant(),
                //       SentAmount      = root.GetProperty("amount_from").GetDecimal(),
                //       ReceivedAsset   = root.GetProperty("to_currency").GetString()?.ToUpperInvariant(),
                //       ReceivedAmount  = root.GetProperty("amount_to").GetDecimal(),
                //       RecipientAddress= root.GetProperty("payout_address").GetString(),
                //       ProviderOrderId = root.GetProperty("id").GetString(),
                //       Note            = $"ghostswap:{statusRaw}",
                //   };
                return OrderProofResult.Unknown("ghostswap: JSON mapping not configured yet");
            }
            catch (JsonException ex)
            {
                return OrderProofResult.Unknown($"ghostswap: bad JSON ({ex.Message})");
            }
        }
    }
}
