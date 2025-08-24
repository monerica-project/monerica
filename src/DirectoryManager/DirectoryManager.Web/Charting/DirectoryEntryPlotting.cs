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

        public byte[] CreateMonthlyActivePlot(List<DirectoryEntriesAudit> audits)
        {
            var monthly = GetMonthlyActiveCounts(audits);
            if (monthly == null || monthly.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var data = monthly
                .OrderBy(m => new DateTime(m.Year, m.Month, 1))
                .Select(m => new { Month = new DateTime(m.Year, m.Month, 1), Count = (double)m.ActiveCount })
                .ToList();

            // spacing & bar width (unchanged from your version)
            const double GAP = 1.12;
            const double BAR_SIZE = 0.72;

            var bars = new List<Bar>(data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                bool grew = i == 0 || data[i].Count >= data[i - 1].Count;
                bars.Add(new Bar
                {
                    Position = i * GAP,
                    Value = data[i].Count,
                    Size = BAR_SIZE,
                    Orientation = Orientation.Vertical,
                    FillColor = grew ? Colors.Green : Colors.Red,
                    LineStyle = new () { Color = Colors.Transparent }
                });
            }

            var plt = new Plot();
            var barPlot = plt.Add.Bars(bars);

            // hide bottom ticks; month text is drawn manually
            plt.Axes.Bottom.IsVisible = false;

            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0, top: 0.14);
            plt.Axes.AutoScale();

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.05, 1); // space for labels above bars
            var lim = plt.Axes.GetLimits();

            // add bottom padding so "0" and the baseline aren't clipped
            double bottomPad = Math.Max(1, maxBar * 0.03);
            double topNeeded = Math.Max(lim.Top, maxBar + (yOffset * 2.0));
            plt.Axes.SetLimitsY(-bottomPad, topNeeded);

            // stacked labels: Value above, Month below (same baseline, opposite alignments)
            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;

                double baseY = y + (yOffset * 0.45);

                var tVal = plt.Add.Text($"{y:0}", x, baseY);
                tVal.Alignment = Alignment.LowerCenter;
                tVal.LabelFontSize = 12;

                var tMon = plt.Add.Text(data[i].Month.ToString("MMM yyyy", CultureInfo.InvariantCulture), x, baseY);
                tMon.Alignment = Alignment.UpperCenter;
                tMon.LabelFontSize = 9;
            }

            plt.Title("Active Directory Entries by Month");
            plt.YLabel("Active Entries");

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
                    int cid = (int)catIdProp.GetValue(sc) !;
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
                            int cid = (int)cidProp.GetValue(catObj) !;
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