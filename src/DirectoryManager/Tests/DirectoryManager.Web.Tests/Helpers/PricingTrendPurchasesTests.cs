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
    }
}
