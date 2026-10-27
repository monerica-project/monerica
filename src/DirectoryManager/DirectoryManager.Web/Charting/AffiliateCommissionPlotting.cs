using System.Globalization;
using DirectoryManager.Data.Models.TransferModels;
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
    }
}
