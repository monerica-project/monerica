using System.Text.Json;

namespace DirectoryManager.TermsWatcher.Watch
{
    public sealed class TermsVerdict
    {
        public bool NeedsUpdate { get; set; }

        public string Summary { get; set; } = string.Empty;

        public List<string> Suggestions { get; set; } = new ();

        public decimal Confidence { get; set; }

        public static TermsVerdict? TryParse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                return new TermsVerdict
                {
                    NeedsUpdate = r.GetProperty("needs_update").GetBoolean(),
                    Summary = (r.GetProperty("summary").GetString() ?? string.Empty).Trim(),
                    Suggestions = r.GetProperty("suggestions").EnumerateArray()
                        .Select(x => (x.GetString() ?? string.Empty).Trim()).Where(x => x.Length > 0).Take(8).ToList(),
                    Confidence = r.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var d) ? Math.Clamp(d, 0m, 1m) : 0m,
                };
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
