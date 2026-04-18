using System.Globalization;
using DirectoryManager.Data.Models.TransferModels;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class SearchAnalyticsPlotting
    {
        public byte[] CreateWeeklySearchTotalsBarChart(
            IEnumerable<WeeklySearchCount> weeklyCounts,
            DateTime? startUtc = null,
            DateTime? endUtc = null)
        {
            if (weeklyCounts is null)
            {
                return Array.Empty<byte>();
            }

            DateTime end = (endUtc ?? DateTime.UtcNow).Date;
            DateTime start = (startUtc ?? end.AddYears(-1)).Date;

            static DateTime StartOfIsoWeek(DateTime d)
            {
                int diff = ((int)d.DayOfWeek + 6) % 7; // Monday=0..Sunday=6
                return d.AddDays(-diff).Date;
            }

            DateTime fromWk = StartOfIsoWeek(start);
            DateTime toWk = StartOfIsoWeek(end);
            if (fromWk > toWk)
            {
                (fromWk, toWk) = (toWk, fromWk);
            }

            // Continuous weeks in range (every Monday)
            var weeks = new List<DateTime>();
            for (var w = fromWk; w <= toWk; w = w.AddDays(7))
            {
                weeks.Add(w);
            }

            if (weeks.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // Aggregate counts by week start
            var countByWeek = weeklyCounts
                .GroupBy(x => StartOfIsoWeek(x.WeekStartUtc))
                .ToDictionary(g => g.Key, g => g.Sum(v => v.Count));

            var data = weeks
                .Select(w => new { Week = w, Count = countByWeek.TryGetValue(w, out var c) ? c : 0 })
                .ToList();

            // Bar spacing
            const double GAP = 1.30;   // >1 => more space between bars
            const double BAR = 0.56;   // thinner bars for readability

            var bars = new List<Bar>(data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                int curr = data[i].Count;
                int prev = i == 0 ? curr : data[i - 1].Count;
                bool grew = curr >= prev;

                bars.Add(new Bar
                {
                    Position = i * GAP,
                    Value = curr,
                    Size = BAR,
                    Orientation = Orientation.Vertical,
                    FillColor = grew ? Colors.Green : Colors.Red,
                    LineStyle = new () { Color = Colors.Transparent }
                });
            }

            var plt = new Plot();
            plt.Add.Bars(bars);

            // --- Ticks that align EXACTLY with bars (stride every Nth bar) ---
            int maxTicks = 14; // target # of x labels (tweak to taste)
            int stride = Math.Max(1, (int)Math.Ceiling((double)weeks.Count / maxTicks));

            var tickPos = new List<double>();
            var tickLab = new List<string>();
            for (int i = 0; i < weeks.Count; i += stride)
            {
                tickPos.Add(i * GAP);
                tickLab.Add(weeks[i].ToString("MMM d", CultureInfo.InvariantCulture));
            }

            // ensure last bar has a label
            double lastPos = (weeks.Count - 1) * GAP;
            if (tickPos.Count == 0 || Math.Abs(tickPos[^1] - lastPos) > 1e-6)
            {
                tickPos.Add(lastPos);
                tickLab.Add(weeks[^1].ToString("MMM d", CultureInfo.InvariantCulture));
            }

            var bottom = plt.Axes.Bottom;
            bottom.IsVisible = true;
            bottom.SetTicks(tickPos.ToArray(), tickLab.ToArray());
            bottom.TickLabelStyle.Rotation = 90;
            bottom.TickLabelStyle.FontSize = 9;
            bottom.MinimumSize = 90; // room for rotated labels

            // 👇 push labels farther below the axis line and reserve space for them
            bottom.TickLabelStyle.OffsetY = 18;   // try 14–22 to taste
            bottom.MinimumSize = 120;             // more room for the rotated labels

            plt.Title("Weekly Searches");
            plt.YLabel("Total Searches");

            // Margins & autoscale
            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0.22, top: 0.18);
            plt.Axes.AutoScale();

            // Floor at 0 and add headroom
            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.05, 1);
            var lim = plt.Axes.GetLimits();
            plt.Axes.SetLimitsY(0, Math.Max(lim.Top, maxBar + (yOffset * 1.8)));

            // Label the last few non-zero bars to avoid clutter
            int labelLastN = Math.Min(10, bars.Count);
            int startLabelAt = Math.Max(0, bars.Count - labelLastN);
            for (int i = startLabelAt; i < bars.Count; i++)
            {
                if (bars[i].Value <= 0)
                {
                    continue;
                }

                double x = bars[i].Position;
                double y = bars[i].Value;

                var tv = plt.Add.Text($"{y:0}", x, y + (yOffset * 0.6));
                tv.Alignment = Alignment.LowerCenter;
                tv.LabelFontSize = 11;
            }

            return plt.GetImageBytes(width: 1200, height: 600, format: ImageFormat.Png);
        }
    }
}