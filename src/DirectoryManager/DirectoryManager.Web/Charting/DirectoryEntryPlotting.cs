using System.Globalization;
using DirectoryManager.Data.Models;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class DirectoryEntryPlotting
    {
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

        // your helper to build the monthly report remains the same...
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

        // adjust to match your “active” statuses
        private static bool IsActiveStatus(int s) =>
            s is (int)Data.Enums.DirectoryStatus.Admitted or
            (int)Data.Enums.DirectoryStatus.Verified or
            (int)Data.Enums.DirectoryStatus.Scam or
            (int)Data.Enums.DirectoryStatus.Questionable;
    }
}