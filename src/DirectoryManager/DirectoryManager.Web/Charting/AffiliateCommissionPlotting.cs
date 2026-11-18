using System.Globalization;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Revenue;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    // Pie chart of affiliate commission USD by listing (server-rendered PNG; the site is no-JS).
    public static class AffiliateCommissionPlotting
    {
        public const int MaxSlices = 10;

        private static readonly string[] Palette =
        {
            "#18b45b", "#ff6600", "#2aa8ff", "#f1c40f", "#9b59b6",
            "#e74c3c", "#1abc9c", "#ff8fc7", "#a3e048", "#c08a5a",
        };

        private const string OtherHex = "#7f8c8d";

        public static string HexFor(int index) => index < Palette.Length ? Palette[index] : OtherHex;

        public static string HexFor(int index, bool isOther) => isOther ? OtherHex : HexFor(index);

        // Biggest earners first; anything past the top MaxSlices - 1 becomes "Other" when there are more than MaxSlices.
        public static List<(string Name, decimal Usd, bool IsOther)> Slices(IEnumerable<AffiliateCommissionEarnedTotal> totals)
        {
            var positive = totals
                .Where(t => t.TotalUsdValue > 0m)
                .OrderByDescending(t => t.TotalUsdValue)
                .Select(t => (Name: string.IsNullOrWhiteSpace(t.DirectoryEntryName) ? "(unknown)" : t.DirectoryEntryName, Usd: t.TotalUsdValue, IsOther: false))
                .ToList();

            if (positive.Count <= MaxSlices)
            {
                return positive;
            }

            var top = positive.Take(MaxSlices - 1).ToList();
            top.Add(($"Other ({positive.Count - (MaxSlices - 1)} listings)", positive.Skip(MaxSlices - 1).Sum(p => p.Usd), true));
            return top;
        }

        public static byte[] Pie(IEnumerable<AffiliateCommissionEarnedTotal> totals)
        {
            var plt = new Plot();
            var data = Slices(totals);
            var sum = data.Sum(d => d.Usd);

            if (data.Count == 0 || sum <= 0m)
            {
                plt.Title("No commissions in this period");
                plt.Axes.Frameless();
                plt.HideGrid();
                return plt.GetDarkImageBytes(900, 520, ImageFormat.Png);
            }

            var usd = CultureInfo.GetCultureInfo("en-US");
            var slices = data.Select((d, i) =>
            {
                var pct = d.Usd / sum * 100m;
                return new PieSlice
                {
                    Value = (double)d.Usd,
                    FillColor = Color.FromHex(d.IsOther ? OtherHex : HexFor(i)),
                    Label = pct >= 4m ? $"{pct.ToString("0.#", CultureInfo.InvariantCulture)}%" : string.Empty,
                    LegendText = $"{d.Name} ({d.Usd.ToString("C0", usd)}, {pct.ToString("0.#", CultureInfo.InvariantCulture)}%)",
                };
            }).ToList();

            var pie = plt.Add.Pie(slices);
            pie.SliceLabelDistance = 0.7;
            pie.DonutFraction = 0.35;
            pie.LineColor = Colors.White;

            plt.Axes.Frameless();
            plt.HideGrid();
            plt.ShowLegend(Edge.Right);
            return plt.GetDarkImageBytes(1000, 560, ImageFormat.Png);
        }

        // Monthly bars (USD): stacked by listing (top listings + "Other"), or a single listing.
        // Months with no commissions are drawn as zero; totals are printed above the bars when they fit.
        public static byte[] MonthlyBars(AffiliateCommissionMonthlyReport.Result result, string? singleCompanyName = null)
        {
            var plt = new Plot();
            var months = result.Months;
            var usd = CultureInfo.GetCultureInfo("en-US");
            var series = AffiliateCommissionMonthlyReport.Series(result);

            for (int s = 0; s < series.Count; s++)
            {
                var bars = new List<Bar>();
                for (int i = 0; i < months.Count; i++)
                {
                    double below = 0;
                    for (int k = 0; k < s; k++)
                    {
                        below += (double)series[k].Values[i];
                    }

                    bars.Add(new Bar
                    {
                        Position = i,
                        ValueBase = below,
                        Value = below + (double)series[s].Values[i],
                        FillColor = Color.FromHex(series[s].Hex),
                        LineWidth = 0,
                    });
                }

                var bp = plt.Add.Bars(bars);
                bp.LegendText = series[s].Name;
            }

            double max = months.Count == 0 ? 0 : months.Max(m => (double)m.Usd);
            if (max <= 0)
            {
                // Keep the zero months visible as an empty axis rather than a blank image.
                plt.Add.Bars(months.Select((m, i) => new Bar { Position = i, Value = 0, LineWidth = 0 }).ToList());
                plt.Axes.SetLimitsY(0, 10);
            }
            else
            {
                if (months.Count <= 24)
                {
                    double pad = max * 0.015;
                    for (int i = 0; i < months.Count; i++)
                    {
                        if (months[i].Usd <= 0m)
                        {
                            continue;
                        }

                        var t = plt.Add.Text(months[i].Usd.ToString("C0", usd), i, (double)months[i].Usd + pad);
                        t.Alignment = Alignment.LowerCenter;
                        t.LabelFontSize = 12;
                        t.LabelFontColor = Colors.White;
                    }
                }

                if (result.AveragePerMonthUsd > 0m)
                {
                    var avg = plt.Add.HorizontalLine((double)result.AveragePerMonthUsd);
                    avg.Color = Color.FromHex("#bbbbbb");
                    avg.LineWidth = 1.5f;
                    avg.LinePattern = LinePattern.Dashed;
                    avg.LegendText = $"Monthly average ({result.AveragePerMonthUsd.ToString("C0", usd)})";
                }

                plt.Axes.SetLimitsY(0, max * 1.12);
            }

            plt.Axes.SetLimitsX(-0.6, Math.Max(months.Count, 1) - 0.4);
            RevenuePlotting.MonthTicks(plt, months.Select(m => m.Month).ToList());
            plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
            {
                LabelFormatter = v => v.ToString("C0", usd),
            };
            plt.Title(singleCompanyName == null
                ? "Affiliate commissions per month (USD)"
                : $"{singleCompanyName}: affiliate commissions per month (USD)");
            plt.YLabel("USD");
            if (series.Count > 0)
            {
                plt.ShowLegend(Edge.Right);
            }

            return plt.GetDarkImageBytes(1400, 600, ImageFormat.Png);
        }
    }
}
