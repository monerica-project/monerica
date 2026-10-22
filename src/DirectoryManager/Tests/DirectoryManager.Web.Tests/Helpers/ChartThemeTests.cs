using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Charting;
using SkiaSharp;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ChartThemeTests
    {
        private static SKColor CornerPixel(byte[] png)
        {
            using var bmp = SKBitmap.Decode(png);
            return bmp.GetPixel(2, 2);
        }

        private static void MaybeSave(string name, byte[] png)
        {
            var dir = Environment.GetEnvironmentVariable("CHART_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                File.WriteAllBytes(Path.Combine(dir, name), png);
            }
        }

        [Fact]
        public void BlackLine_BecomesLightAndStaysVisible()
        {
            var plt = new ScottPlot.Plot();
            var line = plt.Add.Scatter(new double[] { 0, 1, 2 }, new double[] { 1, 3, 2 });
            line.Color = ScottPlot.Colors.Black;
            var hline = plt.Add.HorizontalLine(2);
            hline.Color = ScottPlot.Colors.Black;

            ChartTheme.ApplyDark(plt);

            static double Lum(ScottPlot.Color c) => ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255.0;
            Assert.True(Lum(line.Color) > 0.6, $"scatter colour too dark: {line.Color}");
            Assert.True(Lum(line.LineColor) > 0.6, $"scatter line too dark: {line.LineColor}");
            Assert.True(Lum(hline.LineColor) > 0.6, $"axis line too dark: {hline.LineColor}");
        }

        [Fact]
        public void SubmissionsChart_RendersOnDarkBackground()
        {
            var rnd = new Random(1);
            var subs = Enumerable.Range(0, 400)
                .Select(i => new Submission { CreateDate = new DateTime(2025, 1, 1).AddDays(rnd.Next(0, 640)) })
                .ToList();

            var png = new SubmissionsPlotting().CreateMonthlySubmissionBarChart(subs);
            MaybeSave("submissions.png", png);

            var px = CornerPixel(png);
            Assert.True(px.Red < 30 && px.Green < 30 && px.Blue < 30, $"background not dark: {px}");
        }

        [Fact]
        public void CountryPie_RendersOnDarkBackground()
        {
            var codes = new[] { "US", "US", "US", "DE", "DE", "NL", "CH", "GB", "FR", "CA", "SE", "PL", "RU", "JP" };
            var entries = codes.Select((c, i) => new DirectoryEntry
            {
                DirectoryEntryId = i + 1,
                Name = $"E{i}",
                DirectoryEntryKey = $"e{i}",
                CountryCode = c,
                DirectoryStatus = DirectoryStatus.Verified,
            }).ToList();

            var png = new DirectoryEntryPlotting().CreateActiveCountriesPieChartImage(entries);
            MaybeSave("countries.png", png);

            var px = CornerPixel(png);
            Assert.True(px.Red < 30 && px.Green < 30 && px.Blue < 30, $"background not dark: {px}");
        }
    }
}
