using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Revenue;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    // Server-rendered PNG charts for the admin revenue section (the site is no-JS).
    public static class RevenuePlotting
    {
        private static readonly Dictionary<RevenueSource, string> SourceColors = new ()
        {
            [RevenueSource.SiteSponsors] = "#ff6600",
            [RevenueSource.VideoSponsorships] = "#2aa8ff",
            [RevenueSource.AffiliateCommissions] = "#18b45b",
            [RevenueSource.SubmissionReviews] = "#9b59b6",
            [RevenueSource.VerificationReviews] = "#f1c40f",
            [RevenueSource.ProblemReportReviews] = "#e74c3c",
        };

        public static Color ColorFor(RevenueSource s) => Color.FromHex(SourceColors[s]);

        public static string HexFor(RevenueSource s) => SourceColors[s];

        public static byte[] Pie(RevenueReport report, bool xmr = false)
        {
            var plt = new Plot();
            var slices = report.Totals
                .Where(t => t.AmountUsd > 0m)
                .OrderByDescending(t => t.AmountUsd)
                .Select(t => new PieSlice
                {
                    Value = (double)t.AmountUsd,
                    FillColor = ColorFor(t.Source),
                    Label = t.SharePct >= 4m ? $"{t.SharePct.ToString("0.#", CultureInfo.InvariantCulture)}%" : string.Empty,
                    LegendText = $"{RevenueReportBuilder.Label(t.Source)} ({RevenueMoney.Format(t.AmountUsd, xmr)})",
                })
                .ToList();

            if (slices.Count == 0)
            {
                plt.Title("No revenue in this period");
                plt.Axes.Frameless();
                plt.HideGrid();
                return plt.GetDarkImageBytes(900, 520, ImageFormat.Png);
            }

            var pie = plt.Add.Pie(slices);
            pie.SliceLabelDistance = 0.7;
            pie.DonutFraction = 0.35;
            pie.LineColor = Colors.White;

            plt.Axes.Frameless();
            plt.HideGrid();
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(900, 520, ImageFormat.Png);
        }

        // Stacked monthly bars, one color per source.
        public static byte[] MonthlyStacked(IReadOnlyList<RevenueMonth> months, RevenueBasis basis, bool xmr = false)
        {
            var plt = new Plot();
            if (months.Count == 0)
            {
                plt.Title("No data");
                return plt.GetDarkImageBytes(1400, 600, ImageFormat.Png);
            }

            // Largest source over the whole chart first: bottom of each bar and top of the legend.
            var order = RevenueReportBuilder.Sources
                .OrderByDescending(s => months.Sum(m => m.BySource[s]))
                .ToList();

            foreach (var source in order)
            {
                if (!months.Any(m => m.BySource[source] > 0m))
                {
                    continue;
                }

                var bars = new List<Bar>();
                for (int i = 0; i < months.Count; i++)
                {
                    // Base = everything stacked below this source.
                    double below = order
                        .TakeWhile(s => s != source)
                        .Sum(s => (double)months[i].BySource[s]);
                    double v = (double)months[i].BySource[source];
                    bars.Add(new Bar
                    {
                        Position = i,
                        ValueBase = below,
                        Value = below + v,
                        FillColor = ColorFor(source),
                        LineWidth = 0,
                    });
                }

                var bp = plt.Add.Bars(bars);
                bp.LegendText = RevenueReportBuilder.Label(source);
            }

            MonthTicks(plt, months.Select(m => m.Month).ToList());
            plt.Axes.Margins(bottom: 0, top: 0.1);
            var unit = RevenueMoney.Unit(xmr);
            plt.Title(basis == RevenueBasis.Earned ? $"Revenue earned per month ({unit})" : $"Cash collected per month ({unit})");
            plt.YLabel(unit);
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(1400, 600, ImageFormat.Png);
        }

        // History (earned, grey bars) + forecast: expected line, confidence band, and the
        // contracted floor (money already paid for).
        public static byte[] Forecast(IReadOnlyList<RevenueMonth> history, SponsorshipForecastModel.Result f)
        {
            var plt = new Plot();
            var labels = history.Select(m => m.Month).Concat(f.Months.Select(m => m.MonthStart)).ToList();
            int h = history.Count;

            var histBars = history.Select((m, i) => new Bar
            {
                Position = i,
                Value = (double)m.Total,
                FillColor = Color.FromHex("#bdbdbd"),
                LineWidth = 0,
            }).ToList();
            if (histBars.Count > 0)
            {
                plt.Add.Bars(histBars).LegendText = "Earned (actual)";
            }

            if (f.Months.Count > 0)
            {
                var xs = f.Months.Select((_, i) => (double)(h + i)).ToArray();
                var band = plt.Add.FillY(
                    xs,
                    f.Months.Select(m => (double)m.Low).ToArray(),
                    f.Months.Select(m => (double)m.High).ToArray());
                band.FillColor = Color.FromHex("#1f77b4").WithAlpha(0.18);
                band.LineWidth = 0;
                band.LegendText = $"{(int)Math.Round(f.Confidence * 100)}% range";

                var contracted = plt.Add.Scatter(xs, f.Months.Select(m => (double)m.Contracted).ToArray());
                contracted.Color = Color.FromHex("#2ca02c");
                contracted.LinePattern = LinePattern.Dashed;
                contracted.LineWidth = 2;
                contracted.MarkerSize = 0;
                contracted.LegendText = "Already paid for";

                var exp = plt.Add.Scatter(xs, f.Months.Select(m => (double)m.Expected).ToArray());
                exp.Color = Color.FromHex("#1f77b4");
                exp.LineWidth = 2.5f;
                exp.MarkerSize = 5;
                exp.LegendText = "Expected";

                if (h > 0)
                {
                    var divider = plt.Add.VerticalLine(h - 0.5);
                    divider.Color = Color.FromHex("#999999");
                    divider.LinePattern = LinePattern.Dashed;
                }
            }

            MonthTicks(plt, labels);
            plt.Axes.Margins(bottom: 0, top: 0.1);
            plt.Title("Revenue earned per month: history and forecast (USD)");
            plt.YLabel("USD");
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(1400, 650, ImageFormat.Png);
        }

        // Backtest: what the model predicted (line + band) vs what was earned (bars).
        public static byte[] Backtest(SponsorshipForecastModel.Backtest bt, double confidence)
        {
            var plt = new Plot();
            var xs = bt.Months.Select((_, i) => (double)i).ToArray();

            plt.Add.Bars(bt.Months.Select((m, i) => new Bar
            {
                Position = i,
                Value = (double)m.Actual,
                FillColor = Color.FromHex("#bdbdbd"),
                LineWidth = 0,
            }).ToList()).LegendText = "Actually earned";

            var band = plt.Add.FillY(xs, bt.Months.Select(m => (double)m.Low).ToArray(), bt.Months.Select(m => (double)m.High).ToArray());
            band.FillColor = Color.FromHex("#1f77b4").WithAlpha(0.18);
            band.LineWidth = 0;
            band.LegendText = $"{(int)Math.Round(confidence * 100)}% range";

            var pred = plt.Add.Scatter(xs, bt.Months.Select(m => (double)m.Predicted).ToArray());
            pred.Color = Color.FromHex("#1f77b4");
            pred.LineWidth = 2.5f;
            pred.MarkerSize = 5;
            pred.LegendText = "Predicted (prices known then)";

            var atActual = plt.Add.Scatter(xs, bt.Months.Select(m => (double)m.PredictedAtActualPrices).ToArray());
            atActual.Color = Color.FromHex("#ff6600");
            atActual.LineWidth = 2;
            atActual.LinePattern = LinePattern.Dashed;
            atActual.MarkerSize = 4;
            atActual.LegendText = "Predicted (prices actually charged)";

            MonthTicks(plt, bt.Months.Select(m => m.MonthStart).ToList());
            plt.Axes.Margins(bottom: 0, top: 0.1);
            plt.Title($"Backtest from {bt.AsOfUtc.ToString(DirectoryManager.Common.Constants.StringConstants.MonthYearFormat)}");
            plt.YLabel("USD");
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(1100, 450, ImageFormat.Png);
        }

        private static void MonthTicks(Plot plt, IReadOnlyList<DateTime> months)
        {
            int stride = months.Count <= 18 ? 1 : months.Count <= 36 ? 2 : 3;
            var ticks = new List<Tick>();
            for (int i = 0; i < months.Count; i += stride)
            {
                // Two-line horizontal labels (same style as the invoice charts): never clipped.
                ticks.Add(new Tick(i, months[i].ToString(DirectoryManager.Common.Constants.StringConstants.MonthAbbrFormat, CultureInfo.InvariantCulture) + "\n" + months[i].ToString(DirectoryManager.Common.Constants.StringConstants.YearFormat, CultureInfo.InvariantCulture)));
            }

            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }
    }
}
