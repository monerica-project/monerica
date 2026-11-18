using System.Globalization;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Models.TrustPages
{
    /// <summary>
    /// GET filters of the /incidents page. Junk values fall back to the defaults. The time window
    /// applies only to reviewer reports (open issues are always all shown): default the last
    /// <see cref="DefaultWindowDays"/> days, "all" for all time.
    /// </summary>
    public sealed class IncidentFilter
    {
        /// <summary>Items per page in a single-kind view.</summary>
        public const int PageSize = 25;

        /// <summary>Items per section in the combined view.</summary>
        public const int SectionPreviewSize = 10;

        public const int DefaultWindowDays = 90;

        public static readonly int[] AllowedWindows = { 30, 90, 365 };

        public IncidentKind Kind { get; init; } = IncidentKind.All;

        /// <summary>Reviewer reports from the last N days (30, 90 or 365); null = all time.</summary>
        public int? WindowDays { get; init; } = DefaultWindowDays;

        public int Page { get; init; } = 1;

        public bool IsDefault => this.Kind == IncidentKind.All && this.WindowDays == DefaultWindowDays && this.Page == 1;

        public static IncidentFilter Parse(string? kind, string? days, string? page)
        {
            var k = (kind ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "status" => IncidentKind.Status,
                "reviews" => IncidentKind.Reviews,
                _ => IncidentKind.All,
            };

            var rawDays = (days ?? string.Empty).Trim().ToLowerInvariant();
            int? window = DefaultWindowDays;
            if (rawDays == "all")
            {
                window = null;
            }
            else if (int.TryParse(rawDays, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
                && AllowedWindows.Contains(d))
            {
                window = d;
            }

            // Paging exists only in a single-kind view.
            var p = k == IncidentKind.All ? 1 : PublicReviewFilter.ParsePage(page);

            return new IncidentFilter { Kind = k, WindowDays = window, Page = p };
        }

        /// <summary>Start of the reviewer-report window, or null for all time.</summary>
        public DateTime? SinceUtc(DateTime nowUtc) =>
            this.WindowDays.HasValue ? nowUtc.AddDays(-this.WindowDays.Value) : null;

        /// <summary>
        /// Query string (leading "?" or empty) with defaults left out. The window is written
        /// only for views that show reviewer reports.
        /// </summary>
        public string ToQueryString(IncidentKind? kind = null, int? page = null)
        {
            var k = kind ?? this.Kind;
            var p = page ?? this.Page;
            var parts = new List<string>();
            if (k != IncidentKind.All)
            {
                parts.Add("kind=" + k.ToString().ToLowerInvariant());
            }

            if (k != IncidentKind.Status && this.WindowDays != DefaultWindowDays)
            {
                parts.Add("days=" + (this.WindowDays.HasValue ? this.WindowDays.Value.ToString(CultureInfo.InvariantCulture) : "all"));
            }

            if (k != IncidentKind.All && p > 1)
            {
                parts.Add("page=" + p.ToString(CultureInfo.InvariantCulture));
            }

            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }
    }
}
