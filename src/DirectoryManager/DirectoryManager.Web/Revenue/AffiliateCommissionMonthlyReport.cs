using System.Globalization;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Charting;

namespace DirectoryManager.Web.Revenue
{
    /// <summary>
    /// Affiliate commissions earned, bucketed by calendar month (UTC) for a month range, plus
    /// per-listing totals. Amounts are the recorded UsdValue (USD at the time of the commission),
    /// so payments received in XMR/BTC/USD all add up in one currency, the same as /totals.
    /// </summary>
    public static class AffiliateCommissionMonthlyReport
    {
        public const int MaxMonths = 120; // 10 years
        public const string DefaultRangeKey = "last-12";
        public const int MaxChartCompanies = 8; // top 7 + "Other" when there are more

        public static readonly IReadOnlyList<(string Key, string Label)> RangeOptions = new[]
        {
            ("last-12", "Last 12 months"),
            ("this-year", "This year"),
            ("last-year", "Last year"),
            ("all", "All time"),
            ("custom", "Custom months"),
        };

        public static string MonthLabel(DateTime month) =>
            month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        public static string MonthKey(DateTime month) =>
            month.ToString(DirectoryManager.Common.Constants.StringConstants.YearMonth, CultureInfo.InvariantCulture);

        public static DateTime MonthOf(DateTime d) => new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Parses "yyyy-MM" (what the month selects post). Null when missing or invalid.</summary>
        public static DateTime? ParseMonth(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParseExact(
                value.Trim(),
                DirectoryManager.Common.Constants.StringConstants.YearMonth,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var d) && d.Year >= 2000
                ? MonthOf(d)
                : null;
        }

        /// <summary>
        /// Resolves the range picker into an inclusive month span. Presets ignore from/to; a custom
        /// range (or from/to with no preset) is validated: end is clamped to the current month,
        /// a reversed range is swapped, and anything longer than <see cref="MaxMonths"/> keeps the
        /// latest months. Notices explain every adjustment.
        /// </summary>
        public static MonthRange ResolveRange(string? range, string? from, string? to, DateTime nowUtc, DateTime? firstCommissionUtc)
        {
            var current = MonthOf(nowUtc);
            var key = (range ?? string.Empty).Trim().ToLowerInvariant();
            var fromMonth = ParseMonth(from);
            var toMonth = ParseMonth(to);
            var notices = new List<string>();

            if (key.Length == 0)
            {
                key = fromMonth.HasValue || toMonth.HasValue ? "custom" : DefaultRangeKey;
            }

            if (!RangeOptions.Any(o => o.Key == key))
            {
                notices.Add("Unknown range; showing the last 12 months.");
                key = DefaultRangeKey;
            }

            if (key == "custom")
            {
                if (!fromMonth.HasValue && !toMonth.HasValue)
                {
                    if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
                    {
                        notices.Add("The custom months could not be read; showing the last 12 months.");
                    }

                    key = DefaultRangeKey;
                }
            }

            DateTime start;
            DateTime end;
            switch (key)
            {
                case "this-year":
                    start = new DateTime(current.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    end = current;
                    break;
                case "last-year":
                    start = new DateTime(current.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    end = new DateTime(current.Year - 1, 12, 1, 0, 0, 0, DateTimeKind.Utc);
                    break;
                case "all":
                    start = firstCommissionUtc.HasValue ? MonthOf(firstCommissionUtc.Value) : current;
                    end = current;
                    break;
                case "custom":
                    end = toMonth ?? current;
                    start = fromMonth ?? end.AddMonths(-11);
                    if (fromMonth.HasValue && toMonth.HasValue && start > end)
                    {
                        notices.Add("The start month was after the end month, so they were swapped.");
                        (start, end) = (end, start);
                    }

                    if (end > current)
                    {
                        notices.Add($"The end month was moved back to {MonthLabel(current)} (no later data).");
                        end = current;
                    }

                    break;
                default:
                    end = current;
                    start = current.AddMonths(-11);
                    break;
            }

            if (start > end)
            {
                start = end;
            }

            if (MonthsBetween(start, end) > MaxMonths)
            {
                start = end.AddMonths(-(MaxMonths - 1));
                notices.Add($"Ranges are limited to {MaxMonths / 12} years; showing {MonthLabel(start)} to {MonthLabel(end)}.");
            }

            var label = key == "custom"
                ? $"{MonthLabel(start)} to {MonthLabel(end)}"
                : RangeOptions.First(o => o.Key == key).Label;
            return new MonthRange(key, label, start, end, notices);
        }

        /// <summary>Number of months in the inclusive span start..end.</summary>
        public static int MonthsBetween(DateTime start, DateTime end) =>
            ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;

        /// <summary>
        /// Buckets rows into every month of the range (empty months are zero), totals per listing,
        /// and (when <paramref name="companyId"/> is set) only that listing's rows. Rows outside the
        /// range are ignored.
        /// </summary>
        public static Result Build(IEnumerable<AffiliateCommissionEarnedRow> rows, MonthRange range, int? companyId = null)
        {
            var months = new List<MonthBucket>();
            for (var m = range.StartMonth; m <= range.EndMonth; m = m.AddMonths(1))
            {
                months.Add(new MonthBucket(m));
            }

            var index = months.Select((b, i) => (b.Month, i)).ToDictionary(x => x.Month, x => x.i);
            var companies = new Dictionary<int, CompanyTotal>();

            foreach (var r in rows)
            {
                if (companyId.HasValue && r.DirectoryEntryId != companyId.Value)
                {
                    continue;
                }

                var month = MonthOf(r.CommissionDate.Kind == DateTimeKind.Local ? r.CommissionDate.ToUniversalTime() : r.CommissionDate);
                if (!index.TryGetValue(month, out var i))
                {
                    continue;
                }

                var bucket = months[i];
                bucket.Usd += r.UsdValue;
                bucket.Count++;
                bucket.ByCompany[r.DirectoryEntryId] = bucket.ByCompany.GetValueOrDefault(r.DirectoryEntryId) + r.UsdValue;

                if (!companies.TryGetValue(r.DirectoryEntryId, out var c))
                {
                    c = new CompanyTotal
                    {
                        DirectoryEntryId = r.DirectoryEntryId,
                        Name = string.IsNullOrWhiteSpace(r.DirectoryEntryName) ? "(unknown)" : r.DirectoryEntryName,
                        FirstMonth = month,
                        LastMonth = month,
                    };
                    companies[r.DirectoryEntryId] = c;
                }

                c.Count++;
                c.Usd += r.UsdValue;
                if (month < c.FirstMonth)
                {
                    c.FirstMonth = month;
                }

                if (month > c.LastMonth)
                {
                    c.LastMonth = month;
                }
            }

            var total = months.Sum(m => m.Usd);
            var list = companies.Values
                .OrderByDescending(c => c.Usd)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var c in list)
            {
                c.SharePct = total != 0m ? Math.Round(c.Usd / total * 100m, 1) : 0m;
            }

            return new Result
            {
                Range = range,
                CompanyId = companyId,
                Months = months,
                Companies = list,
                TotalUsd = total,
                TotalCount = months.Sum(m => m.Count),
                AveragePerMonthUsd = months.Count > 0 ? Math.Round(total / months.Count, 2) : 0m,
            };
        }

        /// <summary>
        /// Chart series, largest listing first (bottom of each stacked bar). More than
        /// <see cref="MaxChartCompanies"/> listings → the top ones plus a single "Other".
        /// Colors match <see cref="AffiliateCommissionPlotting"/> (and the table swatches).
        /// </summary>
        public static List<ChartSeries> Series(Result result)
        {
            var positive = result.Companies.Where(c => c.Usd > 0m).ToList();
            bool needOther = positive.Count > MaxChartCompanies;
            var shown = needOther ? positive.Take(MaxChartCompanies - 1).ToList() : positive;

            var series = shown
                .Select((c, i) => new ChartSeries(
                    c.Name,
                    c.DirectoryEntryId,
                    AffiliateCommissionPlotting.HexFor(i),
                    result.Months.Select(m => m.ByCompany.GetValueOrDefault(c.DirectoryEntryId)).ToArray()))
                .ToList();

            if (needOther)
            {
                var shownIds = shown.Select(c => c.DirectoryEntryId).ToHashSet();
                int rest = positive.Count - shown.Count;
                series.Add(new ChartSeries(
                    $"Other ({rest} listings)",
                    null,
                    AffiliateCommissionPlotting.HexFor(0, isOther: true),
                    result.Months.Select(m => m.ByCompany.Where(kv => !shownIds.Contains(kv.Key)).Sum(kv => kv.Value)).ToArray()));
            }

            return series;
        }

        public sealed class MonthRange
        {
            public MonthRange(string key, string label, DateTime startMonth, DateTime endMonth, IReadOnlyList<string> notices)
            {
                this.Key = key;
                this.Label = label;
                this.StartMonth = startMonth;
                this.EndMonth = endMonth;
                this.Notices = notices;
            }

            public string Key { get; }
            public string Label { get; }
            public DateTime StartMonth { get; }
            public DateTime EndMonth { get; } // inclusive (first day of the last month)
            public IReadOnlyList<string> Notices { get; }
            public DateTime FromInclusive => this.StartMonth;
            public DateTime ToExclusive => this.EndMonth.AddMonths(1);
            public int MonthCount => MonthsBetween(this.StartMonth, this.EndMonth);
        }

        public sealed class MonthBucket
        {
            public MonthBucket(DateTime month)
            {
                this.Month = month;
            }

            public DateTime Month { get; }
            public decimal Usd { get; set; }
            public int Count { get; set; }
            public Dictionary<int, decimal> ByCompany { get; } = new ();
        }

        public sealed class CompanyTotal
        {
            public int DirectoryEntryId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Usd { get; set; }
            public decimal SharePct { get; set; }
            public DateTime FirstMonth { get; set; }
            public DateTime LastMonth { get; set; }
        }

        public sealed class ChartSeries
        {
            public ChartSeries(string name, int? directoryEntryId, string hex, decimal[] values)
            {
                this.Name = name;
                this.DirectoryEntryId = directoryEntryId;
                this.Hex = hex;
                this.Values = values;
            }

            public string Name { get; }
            public int? DirectoryEntryId { get; }
            public string Hex { get; }
            public decimal[] Values { get; }
        }

        public sealed class Result
        {
            public MonthRange Range { get; set; } = null!;
            public int? CompanyId { get; set; }
            public List<MonthBucket> Months { get; set; } = new ();
            public List<CompanyTotal> Companies { get; set; } = new ();
            public decimal TotalUsd { get; set; }
            public int TotalCount { get; set; }
            public decimal AveragePerMonthUsd { get; set; }
        }
    }
}
