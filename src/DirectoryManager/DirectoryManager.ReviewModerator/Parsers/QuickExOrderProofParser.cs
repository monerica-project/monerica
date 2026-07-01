using System.Globalization;
using System.Text.Json;
using DirectoryManager.Data.Enums;
using DirectoryManager.ReviewModerator.Abstractions;

namespace DirectoryManager.ReviewModerator.Parsers
{
    /// <summary>
    /// QuickEx (quickex.io) is a Nuxt 3 client-only SPA: the order page (/order/{guid}) ships an
    /// empty shell and fetches the order over a JSON API after hydration, so the HTML is useless.
    /// We therefore call the documented public read endpoint directly:
    ///
    ///   GET https://quickex.io/api/v2/orders/public-info?orderId={id}&destinationAddress={addr}
    ///
    /// This is the destination-gated public variant (no HMAC key): access is authorized by the
    /// receiving address, which is exactly what the reviewer already pastes in the order URL
    /// (`?destinationAddress=bc1q…`). The id in the URL is the order's `legacyOrderId` (a GUID),
    /// not the numeric `orderId`. Verified against the documented v2 contract + a real completed
    /// sample (legacyOrderId 602184da-…, XRP→TRX, completed:true):
    ///
    ///   completed:true + a WITHDRAWAL_COMPLETED orderEvent + a withdrawal with a txId  => terminal success.
    ///   deposits[0]    = what the user SENT     (instrument.currencyTitle + amount; also amountUSDT).
    ///   withdrawals[0] = what the user RECEIVED (instrument.currencyTitle + amount + txId).
    ///   destinationAddress = recipient.   legacyOrderId = ProviderOrderId (matches the review URL).
    ///   CompletedAtUtc = the WITHDRAWAL_COMPLETED event time (fallback: withdrawals[0].createdAt).
    ///
    /// NOTE: all amounts come back as JSON strings ("57"), not numbers — handled below.
    ///
    /// Terminal-FAILURE mapping is deliberately deferred (same posture as StereoSwap): the
    /// documented lifecycle exposes refund/expiry only via endpoints/states we haven't sampled,
    /// so any non-completed order maps to a non-terminal status => the worker retries then flags,
    /// never auto-rejects on a guess. Add refund/expired detection here once a failed sample is seen.
    /// </summary>
    public sealed class QuickExOrderProofParser : IOrderProofParser
    {
        // If a live capture ever shows the frontend passing the GUID as `legacyOrderId` instead of
        // `orderId`, change ONLY this constant. The documented public-info "Try it out" lists the
        // params as (orderId, destinationAddress), so we send the GUID as orderId.
        private const string OrderIdQueryParam = "orderId";

        public IReadOnlyCollection<string> Hosts { get; } = new[] { "quickex.io" };

        // The unlock token (destination address) normally rides in the order URL's query string,
        // so we don't hard-require OrderProofContext; BuildLookupUri falls back to it and only
        // returns null (=> flag) when no address can be found anywhere.
        public bool RequiresUnlockContext => false;

        public Uri? BuildLookupUri(string submittedOrderUrl, string? orderProofContext)
        {
            if (!Uri.TryCreate(submittedOrderUrl?.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Path is /order/{guid} or /{lang}/order/{guid}; grab the segment after "order".
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.FindIndex(segments, s => s.Equals("order", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= segments.Length)
            {
                return null; // not an order URL / no id => worker flags
            }

            var id = segments[idx + 1];

            // Destination address authorizes the read. Prefer the URL query param; fall back to
            // the reviewer-supplied context. No address => we can't fetch => flag.
            var destinationAddress = GetQueryValue(uri.Query, "destinationAddress");
            if (string.IsNullOrWhiteSpace(destinationAddress))
            {
                destinationAddress = orderProofContext?.Trim();
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(destinationAddress))
            {
                return null;
            }

            var endpoint =
                $"https://quickex.io/api/v2/orders/public-info" +
                $"?{OrderIdQueryParam}={Uri.EscapeDataString(id)}" +
                $"&destinationAddress={Uri.EscapeDataString(destinationAddress)}";

            return new Uri(endpoint);
        }

        public OrderProofResult Parse(string responseBody, string contentType)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return OrderProofResult.Unknown("empty body");
            }

            JsonDocument doc;
            JsonElement root;
            try
            {
                doc = JsonDocument.Parse(responseBody);
                root = doc.RootElement;
            }
            catch (JsonException)
            {
                return OrderProofResult.Unknown("order json parse failed");
            }

            using (doc)
            {
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return OrderProofResult.Unknown("unexpected json shape");
                }

                var deposit = First(root, "deposits");
                var withdrawal = First(root, "withdrawals");
                var status = MapStatus(root, withdrawal);

                var sentAsset = InstrumentTitle(deposit) ?? GetString(root, "instrumentFromCurrencyTitle");
                var sentAmount = GetDecimal(deposit, "amount") ?? GetDecimal(root, "claimedDepositAmount");
                var receivedAsset = InstrumentTitle(withdrawal) ?? GetString(root, "instrumentToCurrencyTitle");
                var receivedAmount = GetDecimal(withdrawal, "amount") ?? GetDecimal(root, "amountToGet");

                return new OrderProofResult
                {
                    Status = status,
                    SentAsset = string.IsNullOrWhiteSpace(sentAsset) ? null : sentAsset!.ToUpperInvariant(),
                    SentAmount = sentAmount,
                    ReceivedAsset = string.IsNullOrWhiteSpace(receivedAsset) ? null : receivedAsset!.ToUpperInvariant(),
                    ReceivedAmount = receivedAmount,
                    RecipientAddress = GetString(root, "destinationAddress"),
                    ProviderOrderId = GetString(root, "legacyOrderId")
                                      ?? (root.TryGetProperty("orderId", out var oid) ? oid.ToString() : null),
                    CompletedAtUtc = status == OrderProofStatus.Completed ? CompletedAtUtc(root, withdrawal) : null,
                    Note = "quickex",
                };
            }
        }

        // completed:true + a withdrawal txId (and/or a WITHDRAWAL_COMPLETED event) = terminal success.
        // Anything not-completed maps to a non-terminal status (retry-then-flag); failures deferred.
        private static OrderProofStatus MapStatus(JsonElement root, JsonElement? withdrawal)
        {
            var completedFlag = root.TryGetProperty("completed", out var c)
                && (c.ValueKind == JsonValueKind.True || c.ValueKind == JsonValueKind.False)
                && c.GetBoolean();

            var hasWithdrawalTx = withdrawal is { } w
                && w.TryGetProperty("txId", out var tx)
                && tx.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(tx.GetString());

            var kinds = EventKinds(root);

            if (completedFlag && (hasWithdrawalTx || kinds.Contains("WITHDRAWAL_COMPLETED")))
            {
                return OrderProofStatus.Completed;
            }

            // Informative (but still non-terminal) progress mapping from the latest known event.
            if (kinds.Contains("FUNDS_WITHDRAWAL_START"))
            {
                return OrderProofStatus.Sending;
            }

            if (kinds.Contains("DEPOSIT_REGISTERED") || kinds.Contains("INCOMING_FUNDS_DETECTED"))
            {
                return OrderProofStatus.Confirming;
            }

            if (kinds.Contains("CREATION_END"))
            {
                return OrderProofStatus.AwaitingDeposit;
            }

            return OrderProofStatus.Unknown;
        }

        private static HashSet<string> EventKinds(JsonElement root)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("orderEvents", out var events) && events.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in events.EnumerateArray())
                {
                    var kind = GetString(e, "kind");
                    if (!string.IsNullOrWhiteSpace(kind))
                    {
                        set.Add(kind!);
                    }
                }
            }

            return set;
        }

        private static DateTime? CompletedAtUtc(JsonElement root, JsonElement? withdrawal)
        {
            if (root.TryGetProperty("orderEvents", out var events) && events.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in events.EnumerateArray())
                {
                    if (string.Equals(GetString(e, "kind"), "WITHDRAWAL_COMPLETED", StringComparison.Ordinal))
                    {
                        var when = GetUtc(e, "createdAt");
                        if (when is not null)
                        {
                            return when;
                        }
                    }
                }
            }

            return withdrawal is { } w ? GetUtc(w, "createdAt") : null;
        }

        // deposits[0].instrument.currencyTitle (same nesting for withdrawals[0]).
        private static string? InstrumentTitle(JsonElement? leg)
        {
            if (leg is not { } el || el.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (el.TryGetProperty("instrument", out var instr) && instr.ValueKind == JsonValueKind.Object)
            {
                return GetString(instr, "currencyTitle");
            }

            return null;
        }

        private static JsonElement? First(JsonElement parent, string arrayName)
        {
            if (parent.TryGetProperty(arrayName, out var arr)
                && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    return item; // first element (orders carry 0 or 1)
                }
            }

            return null;
        }

        private static string? GetString(JsonElement parent, string name)
            => parent.ValueKind == JsonValueKind.Object
               && parent.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        // QuickEx returns numeric quantities as JSON strings ("57", "393.07"); accept string or number.
        private static decimal? GetDecimal(JsonElement? parent, string name)
        {
            if (parent is not { } p || p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(name, out var v))
            {
                return null;
            }

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
            {
                return d;
            }

            if (v.ValueKind == JsonValueKind.String
                && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var ds))
            {
                return ds;
            }

            return null;
        }

        private static DateTime? GetUtc(JsonElement parent, string name)
        {
            var s = GetString(parent, name);
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            return DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto)
                ? dto.UtcDateTime
                : null;
        }

        private static string? GetQueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                if (string.Equals(pair[..eq], key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[(eq + 1) ..]);
                }
            }

            return null;
        }
    }
}