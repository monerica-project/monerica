using System.Text.Json;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    /// <summary>The AI's reading of one order page. Parsed strictly; anything malformed is rejected.</summary>
    public sealed class OrderVerdict
    {
        public const string ChecksOut = "checks_out";
        public const string DoesNotCheckOut = "does_not_check_out";
        public const string InProgress = "in_progress";
        public const string CouldNotVerify = "could_not_verify";

        public static readonly string[] Results = { ChecksOut, DoesNotCheckOut, InProgress, CouldNotVerify };

        public static readonly string[] Fits = { "yes", "no", "unclear" };

        public string Result { get; set; } = CouldNotVerify;

        public OrderProofStatus OrderStatus { get; set; } = OrderProofStatus.Unknown;

        public bool OrderVisible { get; set; }

        public bool NeedsUnlock { get; set; }

        public decimal? SentAmount { get; set; }

        public string? SentCoin { get; set; }

        public decimal? ReceivedAmount { get; set; }

        public string? ReceivedCoin { get; set; }

        public decimal? UsdShown { get; set; }

        public string? OrderDate { get; set; }

        public string ReviewFitsOrder { get; set; } = "unclear";

        public List<string> SuggestedTags { get; set; } = new ();

        public List<string> Issues { get; set; } = new ();

        public string Summary { get; set; } = string.Empty;

        public decimal Confidence { get; set; }

        public static OrderVerdict? TryParse(string? json, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty answer";
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var v = new OrderVerdict
                {
                    Result = r.GetProperty("result").GetString() ?? string.Empty,
                    OrderVisible = r.GetProperty("order_visible").GetBoolean(),
                    NeedsUnlock = r.GetProperty("needs_unlock").GetBoolean(),
                    SentAmount = Num(r, "sent_amount"),
                    SentCoin = Str(r, "sent_coin"),
                    ReceivedAmount = Num(r, "received_amount"),
                    ReceivedCoin = Str(r, "received_coin"),
                    UsdShown = Num(r, "usd_shown"),
                    OrderDate = Str(r, "order_date"),
                    ReviewFitsOrder = r.GetProperty("review_fits_order").GetString() ?? "unclear",
                    SuggestedTags = List(r, "suggested_tags"),
                    Issues = List(r, "issues"),
                    Summary = (r.GetProperty("summary").GetString() ?? string.Empty).Trim(),
                    Confidence = Math.Clamp(Num(r, "confidence") ?? 0m, 0m, 1m),
                };
                if (!Results.Contains(v.Result))
                {
                    error = $"unknown result '{v.Result}'";
                    return null;
                }

                if (!Enum.TryParse<OrderProofStatus>(r.GetProperty("order_status").GetString(), out var status)
                    || !Enum.IsDefined(status))
                {
                    error = "unknown order_status";
                    return null;
                }

                v.OrderStatus = status;
                if (!Fits.Contains(v.ReviewFitsOrder))
                {
                    v.ReviewFitsOrder = "unclear";
                }

                return v;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                error = ex.Message;
                return null;
            }
        }

        private static string? Str(JsonElement r, string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim()
                : null;

        private static decimal? Num(JsonElement r, string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d >= 0
                ? d
                : null;

        private static List<string> List(JsonElement r, string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!.Trim()).Where(x => x.Length > 0).ToList()
                : new List<string>();
    }
}
