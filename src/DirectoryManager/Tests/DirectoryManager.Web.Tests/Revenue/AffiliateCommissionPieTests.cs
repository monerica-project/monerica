using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Charting;

namespace DirectoryManager.Web.Tests.Revenue
{
    public class AffiliateCommissionPieTests
    {
        private static AffiliateCommissionEarnedTotal T(string name, decimal usd) =>
            new AffiliateCommissionEarnedTotal { DirectoryEntryName = name, TotalUsdValue = usd };

        [Fact]
        public void TenOrFewer_AllShownLargestFirst_ZeroesDropped()
        {
            var slices = AffiliateCommissionPlotting.Slices(new[] { T("A", 5m), T("B", 50m), T("Z", 0m) });

            Assert.Equal(new[] { "B", "A" }, slices.Select(s => s.Name));
            Assert.DoesNotContain(slices, s => s.IsOther);
        }

        [Fact]
        public void MoreThanTen_RestGroupedAsOther_TotalKept()
        {
            var totals = Enumerable.Range(1, 14).Select(i => T("L" + i, i)).ToList();

            var slices = AffiliateCommissionPlotting.Slices(totals);

            Assert.Equal(AffiliateCommissionPlotting.MaxSlices, slices.Count);
            Assert.True(slices[^1].IsOther);
            Assert.Equal("Other (5 listings)", slices[^1].Name);
            Assert.Equal(totals.Sum(t => t.TotalUsdValue), slices.Sum(s => s.Usd));
        }

        [Fact]
        public void Pie_RendersPng_EvenWhenEmpty()
        {
            var png = AffiliateCommissionPlotting.Pie(Array.Empty<AffiliateCommissionEarnedTotal>());
            Assert.True(png.Length > 100);
            png = AffiliateCommissionPlotting.Pie(new[] { T("A", 5m), T("B", 50m) });
            Assert.True(png.Length > 100);
        }
    }
}
