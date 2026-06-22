using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// StereoSwap is a Next.js app that server-renders the full order object into the page
    /// (React Query dehydration), so a plain GET returns everything — no client-only API call.
    /// The order JSON lives in a flight payload script as an escaped string, e.g.
    ///   ...\"data\":{\"status\":4,...,\"deposit\":{...},\"withdrawal\":{...}},\"dataUpdateCount\":1...
    /// We unescape the \" sequences, slice out the order object, and parse it as real JSON.
    ///
    /// Verified against a real completed sample (id f9f9eff1-…-dbb3980f8901):
    ///   status 4   => the "Success" step (rendered check icon active, "Your XMR was sent!",
    ///                 finished_at set, withdrawal.txid present). UI steps 1..4 are
    ///                 Waiting / Network confirmations / Exchange / Success.
    ///   deposit    = what the user SENT     (BTC 0.03037, deposit.address, deposit.txid in).
    ///   withdrawal = what the user RECEIVED (XMR 5.65987, withdrawal.address = recipient, txid out).
    ///   finished_at => completion timestamp (ISO-8601 UTC).
    ///
    /// Status integers >4 (failures/expired/refunded) aren't known yet, so anything that isn't
    /// 1..4 maps to Unknown => the worker flags for manual review rather than guessing a reject.
    /// Add those codes here once a failed sample is seen.
    ///
    /// The tracker link is keyed by the order id (a public bearer capability; StereoSwap is
    /// account-free), so we fetch anonymously. If an anonymous request ever returns the loading
    /// shell instead of the SSR'd data, Parse finds no object => Unknown => flag (never a false approve).
    /// </summary>
    public sealed class StereoSwapOrderProofParser : IOrderProofParser
    {
        public IReadOnlyCollection<string> Hosts { get; } = new[] { "stereoswap.app" };

        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Path is /{lang}/tracker/{id} or /tracker/{id}; grab the segment after "tracker".
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.FindIndex(segments, s => s.Equals("tracker", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= segments.Length)
            {
                return null; // not a tracker URL / no id => worker flags
            }

            var id = segments[idx + 1];
            return new Uri($"https://stereoswap.app/en/tracker/{Uri.EscapeDataString(id)}");
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("empty body");
            }

            // The order object is embedded escaped inside a Next.js flight <script>. Turn \" into "
            // then slice the dehydrated query's data object (bounded by the unique dataUpdateCount key).
            var unescaped = responseBody.Replace("\\\"", "\"");
            var m = Regex.Match(
                unescaped,
                "\"data\":(\\{\"status\":.*?),\"dataUpdateCount\"",
                RegexOptions.Singleline);

            if (!m.Success)
            {
                return OrderProofResult.Unknown("no embedded order json");
            }

            JsonElement root;
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(m.Groups[1].Value);
                root = doc.RootElement;
            }
            catch (JsonException)
            {
                return OrderProofResult.Unknown("order json parse failed");
            }

            using (doc)
            {
                var status = MapStatus(GetInt(root, "status"));
                var deposit = TryGetObject(root, "deposit");
                var withdrawal = TryGetObject(root, "withdrawal");

                return new OrderProofResult
                {
                    Status = status,
                    SentAsset = CoinToken(deposit),
                    SentAmount = GetDecimal(deposit, "amount"),
                    ReceivedAsset = CoinToken(withdrawal),
                    ReceivedAmount = GetDecimal(withdrawal, "amount"),
                    RecipientAddress = GetString(withdrawal, "address"),
                    ProviderOrderId = GetString(root, "id"),
                    CompletedAtUtc = status == OrderProofStatus.Completed ? GetUtc(root, "finished_at") : null,
                    Note = "stereoswap",
                };
            }
        }

        private static OrderProofStatus MapStatus(int? status) => status switch
        {
            1 => OrderProofStatus.AwaitingDeposit,
            2 => OrderProofStatus.Confirming,
            3 => OrderProofStatus.Exchanging,
            4 => OrderProofStatus.Completed,
            _ => OrderProofStatus.Unknown,
        };

        // {"coin_network":{"coin":{"token":"BTC",...}}}
        private static string? CoinToken(JsonElement? leg)
        {
            if (leg is null)
            {
                return null;
            }

            var token = TryGetObject(leg.Value, "coin_network");
            token = token is null ? null : TryGetObject(token.Value, "coin");
            var s = token is null ? null : GetString(token.Value, "token");
            return string.IsNullOrWhiteSpace(s) ? null : s!.ToUpperInvariant();
        }

        private static JsonElement? TryGetObject(JsonElement parent, string name)
            => parent.ValueKind == JsonValueKind.Object
               && parent.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.Object
                ? v
                : null;

        private static int? GetInt(JsonElement parent, string name)
            => parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                ? i
                : null;

        private static decimal? GetDecimal(JsonElement? parent, string name)
            => parent is { } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)
                ? d
                : null;

        private static string? GetString(JsonElement? parent, string name)
            => parent is { } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static DateTime? GetUtc(JsonElement parent, string name)
        {
            var s = GetString(parent, name);
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
                ? dto.UtcDateTime
                : null;
        }
    }
}