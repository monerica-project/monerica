using System.Globalization;
using DirectoryManager.Common.Constants;
using DirectoryManager.Web.Activity;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    // Server-rendered PNG charts for the admin activity report (the site is no-JS):
    // one stacked bar per week or month, one color per kind of submission.
    public static class ActivityPlotting
    {
        public static byte[] Stacked(IReadOnlyList<ActivityBucket> buckets, ActivityGranularity granularity)
        {
            var plt = new Plot();
            var what = granularity == ActivityGranularity.Week ? "week" : "month";

            if (buckets.Count == 0 || buckets.All(b => b.Total == 0))
            {
                plt.Title($"No submissions in this period (per {what})");
                return plt.GetDarkImageBytes(1400, 560, ImageFormat.Png);
            }

            var order = ActivityReportBuilder.Types;
            foreach (var type in order)
            {
                if (!buckets.Any(b => b.Counts[type] > 0))
                {
                    continue;
                }

                var bars = new List<Bar>();
                for (int i = 0; i < buckets.Count; i++)
                {
                    // Base = everything stacked below this type in the same bar.
                    double below = order.TakeWhile(t => t != type).Sum(t => (double)buckets[i].Counts[t]);
                    bars.Add(new Bar
                    {
                        Position = i,
                        ValueBase = below,
                        Value = below + buckets[i].Counts[type],
                        FillColor = Color.FromHex(ActivityReportBuilder.Hex(type)),
                        LineWidth = 0,
                    });
                }

                plt.Add.Bars(bars).LegendText = ActivityReportBuilder.Label(type);
            }

            Ticks(plt, buckets, granularity);

            // Counts are whole numbers: integer ticks only.
            plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { IntegerTicksOnly = true };
            plt.Axes.Margins(bottom: 0, top: 0.1);
            plt.Title(granularity == ActivityGranularity.Week ? "Submissions per week (weeks start Monday)" : "Submissions per month");
            plt.YLabel("Submissions");
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(1400, 560, ImageFormat.Png);
        }

        private static void Ticks(Plot plt, IReadOnlyList<ActivityBucket> buckets, ActivityGranularity granularity)
        {
            // Keep labels readable: at most ~18 labels across the axis.
            int stride = Math.Max(1, (int)Math.Ceiling(buckets.Count / 18.0));
            var ticks = new List<Tick>();
            for (int i = 0; i < buckets.Count; i += stride)
            {
                var start = buckets[i].Start;
                var top = granularity == ActivityGranularity.Week
                    ? start.ToString(StringConstants.MonthDayFormat, CultureInfo.InvariantCulture)
                    : start.ToString(StringConstants.MonthAbbrFormat, CultureInfo.InvariantCulture);
                ticks.Add(new Tick(i, top + "\n" + start.ToString(StringConstants.YearFormat, CultureInfo.InvariantCulture)));
            }

            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }
    }
}
