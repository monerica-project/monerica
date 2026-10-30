using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    // Pricing chart purchase markers: a prepaid renewal shows on the day it was bought,
    // even though its campaign starts later.
    public class PricingTrendPurchasesTests
    {
        private static readonly DateTime Today = new (2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);

        private static SponsoredListingInvoice Inv(SponsorshipType type, decimal usd, DateTime bought, DateTime start, int days) => new ()
        {
            SponsorshipType = type,
            Amount = usd,
            Currency = Currency.USD,
            PaymentStatus = PaymentStatus.Paid,
            CreateDate = bought,
            CampaignStartDate = start,
            CampaignEndDate = start.AddDays(days - 1),
        };

        [Fact]
        public void Prepaid_renewal_shows_on_the_day_it_was_bought()
        {
            var invoices = new[]
            {
                Inv(SponsorshipType.CategorySponsor, 3060m, Today.AddHours(15), Today.AddDays(26), 180), // starts next month
                Inv(SponsorshipType.CategorySponsor, 675m, Today.AddDays(-66), Today.AddDays(-64), 90),
                Inv(SponsorshipType.MainSponsor, 900m, Today, Today, 30),                              // other type
                Inv(SponsorshipType.CategorySponsor, 500m, Today.AddDays(-400), Today.AddDays(-400), 30), // before range
            };

            var purchases = PricingTrendCalculator.Purchases(invoices, Currency.USD, Today.AddDays(-180), Today, SponsorshipType.CategorySponsor);

            Assert.Equal(2, purchases.Count);
            Assert.Equal(Today, purchases[1].Day);
            Assert.Equal(17m, purchases[1].PerDay);
            Assert.Equal(7.5m, purchases[0].PerDay);
        }

        [Fact]
        public void Chart_draws_with_purchase_markers()
        {
            var invoices = new[]
            {
                Inv(SponsorshipType.CategorySponsor, 675m, Today.AddDays(-66), Today.AddDays(-64), 90),
                Inv(SponsorshipType.CategorySponsor, 3060m, Today, Today.AddDays(26), 180),
            };

            var png = new InvoicePlotting().CreateDailyAvgPriceByTypeChart(
                invoices, Currency.USD, Today.AddDays(-180), Today, SponsorshipType.CategorySponsor,
                width: 820, height: 340, darkTheme: true, showPurchases: true);

            Assert.NotEmpty(png);
        }

        [Fact]
        public void Market_purchase_uses_list_price_before_personal_discount()
        {
            // Paid $2,397.75 after a 10% perk; the spot was listed at $2,664.17 for 30 days.
            var start = Today.AddDays(8);
            var inv = new SponsoredListingInvoice
            {
                SponsorshipType = SponsorshipType.MainSponsor,
                Amount = 2397.75m,
                OriginalAmount = 2664.17m,
                DiscountPercent = 10m,
                Currency = Currency.USD,
                PaymentStatus = PaymentStatus.Paid,
                CreateDate = Today,
                CampaignStartDate = start,
                CampaignEndDate = start.AddDays(30),
            };

            var p = Assert.Single(PricingTrendCalculator.MarketPurchases(new[] { inv }, Currency.USD, Today.AddDays(-180), Today, SponsorshipType.MainSponsor));

            Assert.Equal(Today, p.Day);                     // shows the day it was bought, not when it starts
            Assert.Equal(30, p.Days);
            Assert.Equal(88.81m, Math.Round(p.ListPerDay, 2)); // same as today's 30-day price
            Assert.True(p.IsMonthly);
        }

        [Fact]
        public void Market_purchase_without_discount_uses_amount_paid_and_flags_long_terms()
        {
            var start = Today.AddDays(-10);
            var inv = new SponsoredListingInvoice
            {
                SponsorshipType = SponsorshipType.MainSponsor,
                Amount = 17520m,
                Currency = Currency.USD,
                PaymentStatus = PaymentStatus.Paid,
                CreateDate = start,
                CampaignStartDate = start,
                CampaignEndDate = start.AddDays(365),
            };

            var p = Assert.Single(PricingTrendCalculator.MarketPurchases(new[] { inv }, Currency.USD, Today.AddDays(-180), Today, SponsorshipType.MainSponsor));

            Assert.Equal(48m, p.ListPerDay);
            Assert.False(p.IsMonthly);
        }

        [Fact]
        public void Market_price_chart_draws_overview_and_single_placement()
        {
            var invoices = new[]
            {
                Inv(SponsorshipType.MainSponsor, 2664m, Today, Today.AddDays(8), 31),
                Inv(SponsorshipType.CategorySponsor, 675m, Today.AddDays(-66), Today.AddDays(-64), 91),
            };
            var now = new Dictionary<SponsorshipType, decimal>
            {
                [SponsorshipType.MainSponsor] = 88.81m,
                [SponsorshipType.CategorySponsor] = 3.33m,
                [SponsorshipType.SubcategorySponsor] = 2m,
            };

            var plotting = new InvoicePlotting();
            Assert.NotEmpty(plotting.CreateMarketPriceChart(invoices, Currency.USD, Today.AddDays(-180), Today, null, now));
            Assert.NotEmpty(plotting.CreateMarketPriceChart(invoices, Currency.USD, Today.AddDays(-180), Today, SponsorshipType.MainSponsor, now, width: 820, height: 420, highlightInvoices: invoices.Take(1), highlightLabel: "Mine"));
        }

        [Fact]
        public void Market_price_chart_is_empty_with_no_purchases_and_no_price()
        {
            var png = new InvoicePlotting().CreateMarketPriceChart(
                Array.Empty<SponsoredListingInvoice>(), Currency.USD, Today.AddDays(-180), Today, SponsorshipType.MainSponsor, null);

            Assert.Empty(png);
        }
    }
}
