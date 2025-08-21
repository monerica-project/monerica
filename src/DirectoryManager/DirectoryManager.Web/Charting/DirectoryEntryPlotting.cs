using System.Globalization;
using DirectoryManager.Data.Models;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class DirectoryEntryPlotting
    {
        public static List<(int Year, int Month, int ActiveCount)> GetMonthlyActiveCounts(
            List<DirectoryEntriesAudit> audits)
        {
            if (audits == null || audits.Count == 0)
            {
                throw new ArgumentException("no audit data");
            }

            // build a history per entry, using UpdateDate if present
            var byEntry = audits
                .Select(a => new
                {
                    a.DirectoryEntryId,
                    EffectiveDate = a.UpdateDate ?? a.CreateDate,
                    a.DirectoryStatus
                })
                .GroupBy(x => x.DirectoryEntryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.EffectiveDate).ToList());

            // compute full month span
            DateTime minDate = byEntry.Values.SelectMany(v => v).Min(x => x.EffectiveDate);
            DateTime start = new DateTime(minDate.Year, minDate.Month, 1);
            DateTime end = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            int months = ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;

            var report = new List<(int Year, int Month, int ActiveCount)>();
            for (int i = 0; i < months; i++)
            {
                DateTime monthStart = start.AddMonths(i);
                DateTime cutoff = monthStart.AddMonths(1).AddTicks(-1);

                int activeThisMonth = byEntry.Count(kvp =>
                {
                    var record = kvp.Value
                        .Where(x => x.EffectiveDate <= cutoff)
                        .LastOrDefault();
                    return record != null && IsActiveStatus((int)record.DirectoryStatus);
                });

                report.Add((monthStart.Year, monthStart.Month, activeThisMonth));
            }

            return report;
        }

        /// <summary>
        /// Single‐bar‐per‐month showing how many entries were active at month‐end.
        /// </summary>
        /// <returns>bytes.</returns>
        public byte[] CreateMonthlyActivePlot(List<DirectoryEntriesAudit> audits)
        {
            // build the month‐end counts
            var monthly = GetMonthlyActiveCounts(audits);
            int n = monthly.Count;
            double[] positions = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            double[] heights = monthly.Select(x => (double)x.ActiveCount).ToArray();
            string[] labels = monthly
                .Select(x => new DateTime(x.Year, x.Month, 1)
                    .ToString("MMM yyyy", CultureInfo.InvariantCulture))
                .ToArray();

            var plt = new Plot();
            plt.Add.HorizontalLine(0, color: Colors.Transparent);

            // build Bar objects with red/green fill
            var bars = new List<Bar>();
            for (int i = 0; i < n; i++)
            {
                bool grew = i == 0 || heights[i] >= heights[i - 1];
                bars.Add(new Bar()
                {
                    Position = positions[i],
                    Value = heights[i],
                    FillColor = grew ? Colors.Green
                                     : Colors.Red,
                    LineStyle = new () { Color = Colors.Transparent } // no border
                });
            }

            // add them all at once
            var barPlot = plt.Add.Bars(bars);

            // force y≥0
            plt.Axes.AutoScale();
            var limits = plt.Axes.GetLimits();
            plt.Axes.SetLimits(limits.Left, limits.Right, 0, limits.Top);

            // bottom axis ticks & labels
            var bot = plt.Axes.Bottom;
            bot.MinimumSize = 100;
            bot.TickLabelStyle.Rotation = 90;
            bot.TickLabelStyle.Alignment = Alignment.LowerCenter;
            bot.TickLabelStyle.OffsetY = 30;
            bot.SetTicks(positions, labels);
            bot.Label.Text = "Month";
            bot.Label.FontSize = 14;
            bot.Label.OffsetY = 100;

            // title & legend
            plt.Title("Active Directory Entries by Month");
            plt.YLabel("Active Entries");
            var legend = plt.ShowLegend(Edge.Bottom);
            legend.Alignment = Alignment.UpperRight;

            // layout & export
            plt.Layout.Default();
            return plt.GetImageBytes(width: 1200, height: 600, format: ImageFormat.Png);
        }

        public byte[] CreateCategoryPieChartImage(
            IEnumerable<DirectoryEntry> entries,
            IEnumerable<Category> categories)
        {
            if (entries is null || !entries.Any())
            {
                return [];
            }

            // CategoryId -> Display label (prefer Name, fall back to CategoryKey)
            var catLabelById = (categories ?? Enumerable.Empty<Category>())
                .ToDictionary(
                    c => c.CategoryId,
                    c => !string.IsNullOrWhiteSpace(c.Name) ? c.Name
                         : (!string.IsNullOrWhiteSpace(c.CategoryKey) ? c.CategoryKey
                         : $"Category {c.CategoryId}"));

            // Resolve CategoryId from DirectoryEntry.SubCategory (works whether CategoryId or Category is populated)
            int? CategoryIdFor(DirectoryEntry e)
            {
                if (e.SubCategory is null)
                {
                    return null;
                }

                // SubCategory.CategoryId (int)?
                var sc = e.SubCategory;
                var catIdProp = sc.GetType().GetProperty("CategoryId");
                if (catIdProp is not null && catIdProp.PropertyType == typeof(int))
                {
                    int cid = (int)catIdProp.GetValue(sc)!;
                    if (cid > 0)
                    {
                        return cid;
                    }
                }

                // SubCategory.Category.CategoryId (int)?
                var catProp = sc.GetType().GetProperty("Category");
                if (catProp is not null)
                {
                    var catObj = catProp.GetValue(sc);
                    if (catObj is not null)
                    {
                        var cidProp = catObj.GetType().GetProperty("CategoryId");
                        if (cidProp is not null && cidProp.PropertyType == typeof(int))
                        {
                            int cid = (int)cidProp.GetValue(catObj)!;
                            if (cid > 0)
                            {
                                return cid;
                            }
                        }
                    }
                }

                return null;
            }

            // Build counts per Category label
            var breakdown = entries
                .GroupBy(e =>
                {
                    var cid = CategoryIdFor(e);
                    if (cid.HasValue && catLabelById.TryGetValue(cid.Value, out var name))
                    {
                        return name;
                    }

                    return "Uncategorized";
                })
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToArray();

            if (breakdown.Length == 0)
            {
                return Array.Empty<byte>();
            }

            double total = breakdown.Sum(x => (double)x.Count);

            // D3 Category20/20b-like palette
            var hexPalette = new[]
            {
        "#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd",
        "#8c564b", "#e377c2", "#7f7f7f", "#bcbd22", "#17becf",
        "#393b79", "#637939", "#8c6d31", "#843c39", "#7b4173",
        "#3182bd", "#31a354", "#756bb1", "#636363", "#e6550d",

        "#393b79", "#5254a3", "#6b6ecf", "#9c9ede", "#637939",
        "#8ca252", "#b5cf6b", "#cedb9c", "#8c6d31", "#bd9e39",
        "#e7ba52", "#e7cb94", "#843c39", "#ad494a", "#d6616b",
        "#e7969c", "#7b4173", "#a55194", "#ce6dbd", "#de9ed6"
            };
            var palette = hexPalette.Select(ScottPlot.Color.FromHex).ToArray();

            // Build slices with % labels and legend text = category label
            var slices = breakdown
                .Select((item, idx) =>
                {
                    double pct = total > 0 ? Math.Round(item.Count * 100.0 / total, 1) : 0;
                    var sliceColor = palette[idx % palette.Length];
                    return new ScottPlot.PieSlice(item.Count, sliceColor, $"{pct}%")
                    {
                        LegendText = item.Label
                    };
                })
                .ToArray();

            var plt = new ScottPlot.Plot();

            // Match the pie style from your InvoicePlotting example
            plt.HideAxesAndGrid();

            var pie = plt.Add.Pie(slices);
            pie.DonutFraction = 0;          // full pie
            pie.SliceLabelDistance = 1.2;   // labels just outside slices
            pie.Rotation = ScottPlot.Angle.FromDegrees(-90); // largest slice starts at 12 o'clock

            plt.Title("Active Entries by Category");
            plt.ShowLegend(ScottPlot.Edge.Right);

            return plt.GetImageBytes(width: 900, height: 700, format: ScottPlot.ImageFormat.Png);
        }

        // adjust to match your “active” statuses
        private static bool IsActiveStatus(int s) =>
            s is (int)Data.Enums.DirectoryStatus.Admitted or
            (int)Data.Enums.DirectoryStatus.Verified or
            (int)Data.Enums.DirectoryStatus.Scam or
            (int)Data.Enums.DirectoryStatus.Questionable;
    }
}