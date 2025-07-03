using DirectoryManager.Data.Models;
using ScottPlot;
using System.Globalization;

namespace DirectoryManager.Web.Charting
{
    public class DirectoryEntryPlotting
    {

        /// <summary>
        /// Single‐bar‐per‐month showing how many entries were active at month‐end.
        /// </summary>
        /// <returns></returns>
        public byte[] CreateMonthlyActivePlot(List<DirectoryEntriesAudit> audits)
        {
            // 1) get the Year/Month → ActiveCount series
            var monthly = GetMonthlyActiveCounts(audits);

            int n = monthly.Count;
            double[] positions = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            double[] heights = monthly.Select(x => (double)x.ActiveCount).ToArray();
            string[] labels = monthly
                .Select(x => new DateTime(x.Year, x.Month, 1)
                    .ToString("MMM yyyy", CultureInfo.InvariantCulture))
                .ToArray();

            // 2) build the plot
            var plt = new Plot();

            plt.Add.HorizontalLine(0, color: Colors.Transparent);

            // create one Bar per month at its x‐position, height = active count
            var bars = positions
                .Zip(heights, (pos, val) => new Bar()
                {
                    Position = pos,
                    Value = val,
                    FillColor = plt.Add.Palette.GetColor(0)
                })
                .ToList();

            var bp = plt.Add.Bars(bars);
            bp.LegendText = "Active entries";

            plt.Axes.AutoScale();

            var limits = plt.Axes.GetLimits();
            plt.Axes.SetLimits(
                left: limits.Left,
                right: limits.Right,
                bottom: 0,
                top: limits.Top);

            // 3) configure the bottom axis
            var bot = plt.Axes.Bottom;

            // a) make room for the tick labels + the title
            bot.MinimumSize = 100;

            // b) rotate & align the tick labels
            bot.TickLabelStyle.Rotation = 90;
            bot.TickLabelStyle.Alignment = Alignment.LowerCenter;
            bot.TickLabelStyle.OffsetY = 30;    // push them down under the zero line

            // c) apply our lower-case tick texts
            bot.SetTicks(positions, labels);

            // d) set the axis‐title lower-case and push it down
            bot.Label.Text = "Month";
            bot.Label.FontSize = 14;
            bot.Label.OffsetY = 10;    // drop the “month” label further down

            // 4) titles & legend
            plt.Title("Active Directory Entries by Month");
            plt.YLabel("Active Entries");
            var legendPanel = plt.ShowLegend(Edge.Bottom);
            legendPanel.Alignment = Alignment.UpperRight;

            // 5) re-apply automatic layout so all panels draw in the right order
            plt.Layout.Default();

            // 7) export 1200×600 PNG
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