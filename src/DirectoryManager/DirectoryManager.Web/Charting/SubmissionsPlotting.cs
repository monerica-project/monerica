using System.Globalization;
using DirectoryManager.Data.Models;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class SubmissionsPlotting
    {
        public byte[] CreateMonthlySubmissionBarChart(IEnumerable<Submission> submissions)
        {
            if (submissions is null)
            {
                return Array.Empty<byte>();
            }

            var items = submissions.ToList();
            if (items.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // Build a continuous month range (fill missing months with 0)
            var minMonth = new DateTime(items.Min(s => s.CreateDate).Year, items.Min(s => s.CreateDate).Month, 1);
            var maxMonth = new DateTime(items.Max(s => s.CreateDate).Year, items.Max(s => s.CreateDate).Month, 1);

            var months = new List<DateTime>();
            for (var m = minMonth; m <= maxMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            var countsByMonth = months
                .Select(m => new
                {
                    Month = m,
                    Count = items.Count(s => s.CreateDate.Year == m.Year && s.CreateDate.Month == m.Month)
                })
                .ToList();

            // Bar spacing similar to your other charts
            const double GAP = 0.30;           // space between bars
            const double BAR_SIZE = 1.0 - GAP; // visual width of each bar

            var bars = new List<Bar>(countsByMonth.Count);
            for (int i = 0; i < countsByMonth.Count; i++)
            {
                int curr = countsByMonth[i].Count;
                int prev = i == 0 ? curr : countsByMonth[i - 1].Count;
                bool grew = curr >= prev;

                bars.Add(new Bar
                {
                    Position = i,
                    Value = curr,
                    Size = BAR_SIZE,
                    FillColor = grew ? Colors.Black : Colors.Black,
                    LineStyle = new () { Color = Colors.Transparent } // no border
                });
            }

            var plt = new Plot();
            var barPlot = plt.Add.Bars(bars);

            // Hide bottom ticks; we add our own labels above the bars
            plt.Axes.Bottom.IsVisible = false;

            // Titles
            plt.Title("Submissions by Month");
            plt.YLabel("Count");

            // Margins & autoscale
            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0, top: 0.12);
            plt.Axes.AutoScale();

            // Add headroom for labels and a tiny bottom pad so "0" isn't clipped
            var lim = plt.Axes.GetLimits();
            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double labelHeadroom = Math.Max(maxBar * 0.05, 1);          // space above bars for text
            double neededTop = Math.Max(lim.Top, maxBar + (labelHeadroom * 1.4));
            double bottomPad = Math.Max(1, maxBar * 0.03);               // small white space below zero

            plt.Axes.SetLimitsY(-bottomPad, neededTop);

            // Text labels: value above, month just below it (centered over each bar)
            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;

                string month = countsByMonth[i].Month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                double baseY = y + (labelHeadroom * 0.6);

                var tVal = plt.Add.Text($"{y:0}", x, baseY);
                tVal.Alignment = Alignment.LowerCenter;   // sits above base line
                tVal.LabelFontSize = 12;

                var tMon = plt.Add.Text(month, x, baseY);
                tMon.Alignment = Alignment.UpperCenter;   // sits below base line
                tMon.LabelFontSize = 9;                   // smaller month text
            }

            return plt.GetImageBytes(width: 1200, height: 600, format: ImageFormat.Png);
        }
    }
}