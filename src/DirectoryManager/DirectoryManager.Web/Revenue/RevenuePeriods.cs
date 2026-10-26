using System.Globalization;

namespace DirectoryManager.Web.Revenue
{
    // Named reporting periods (all UTC, end exclusive).
    public static class RevenuePeriods
    {
        public static readonly IReadOnlyList<(string Key, string Label)> Options = new[]
        {
            ("this-month", "This month"),
            ("last-month", "Last month"),
            ("last-30", "Last 30 days"),
            ("last-90", "Last 90 days"),
            ("this-quarter", "This quarter"),
            ("ytd", "Year to date"),
            ("last-year", "Last calendar year"),
            ("last-12", "Last 12 months"),
            ("all", "All time"),
            ("custom", "Custom dates"),
        };

        public static (string Key, string Label, DateTime From, DateTime To) Resolve(string? period, DateTime? from, DateTime? to, DateTime nowUtc)
        {
            var today = nowUtc.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var key = (period ?? "this-month").Trim().ToLowerInvariant();

            (DateTime f, DateTime t) = key switch
            {
                "last-month" => (monthStart.AddMonths(-1), monthStart),
                "last-30" => (tomorrow.AddDays(-30), tomorrow),
                "last-90" => (tomorrow.AddDays(-90), tomorrow),
                "this-quarter" => (new DateTime(today.Year, (((today.Month - 1) / 3) * 3) + 1, 1, 0, 0, 0, DateTimeKind.Utc), tomorrow),
                "ytd" => (new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), tomorrow),
                "last-year" => (new DateTime(today.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                "last-12" => (monthStart.AddMonths(-12), monthStart),
                "all" => (new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), tomorrow),
                "custom" when from.HasValue => (
                    DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc),
                    DateTime.SpecifyKind((to ?? today).Date.AddDays(1), DateTimeKind.Utc)),
                _ => (monthStart, tomorrow),
            };

            if (t <= f)
            {
                t = f.AddDays(1);
            }

            if (!Options.Any(o => o.Key == key) || (key == "custom" && !from.HasValue))
            {
                key = "this-month";
            }

            var label = key == "custom"
                ? $"{f.ToString(DirectoryManager.Common.Constants.StringConstants.DateFormat, CultureInfo.InvariantCulture)} to {t.AddDays(-1).ToString(DirectoryManager.Common.Constants.StringConstants.DateFormat, CultureInfo.InvariantCulture)}"
                : Options.First(o => o.Key == key).Label;
            return (key, label, f, t);
        }
    }
}
