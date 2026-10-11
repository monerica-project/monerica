using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Implementations;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    public class VideoSponsorshipTests
    {
        private static readonly DateTime Now = new (2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        // ---- Pricing ----
        [Theory]
        [InlineData(1000, 1, 0, 1000, 0, 1000)]
        [InlineData(1000, 3, 0, 3000, 0, 3000)]
        [InlineData(1000, 1, 10, 1000, 100, 900)]
        [InlineData(1000, 2, 15, 2000, 300, 1700)]
        [InlineData(2500, 1, 20, 2500, 500, 2000)]
        [InlineData(999.99, 3, 15, 2999.97, 450.00, 2549.97)]
        public void Compute_PricesOrder(decimal unit, int qty, decimal pct, decimal original, decimal discount, decimal total)
        {
            var result = VideoSponsorPricing.Compute(unit, qty, pct);

            Assert.Equal(original, result.Original);
            Assert.Equal(discount, result.DiscountAmount);
            Assert.Equal(total, result.Total);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Compute_RejectsNonPositiveQuantity(int qty)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VideoSponsorPricing.Compute(1000m, qty, 0m));
        }

        [Fact]
        public void Compute_RejectsNegativePrice()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VideoSponsorPricing.Compute(-1m, 1, 0m));
        }

        [Theory]
        [InlineData(3000, 3)]
        [InlineData(2549.97, 3)]
        [InlineData(1000, 3)]
        [InlineData(100.01, 2)]
        [InlineData(900, 1)]
        public void SplitAcrossItems_AlwaysAddsBackToTotal(decimal total, int qty)
        {
            var parts = VideoSponsorPricing.SplitAcrossItems(total, qty);

            Assert.Equal(qty, parts.Count);
            Assert.Equal(total, parts.Sum());
            Assert.True(parts.Max() - parts.Min() <= 0.01m);
        }

        // ---- Product demo cost: over $100 is charged in full, otherwise nothing ----
        [Theory]
        [InlineData("250", 250)]
        [InlineData("100.01", 100.01)]
        [InlineData("$1,250.50", 1250.50)]
        [InlineData(" 300 ", 300)]
        public void DemoCost_OverThreshold_IsAccepted(string input, decimal expected)
        {
            Assert.True(VideoSponsorPricing.TryResolveDemoCost(true, input, out var cost, out var error));
            Assert.Equal(expected, cost);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("100")]
        [InlineData("99.99")]
        [InlineData("0")]
        [InlineData("-500")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("abc")]
        [InlineData("1e5")]
        [InlineData("100001")]
        public void DemoCost_SaidYesButInvalidAmount_IsRejected(string? input)
        {
            Assert.False(VideoSponsorPricing.TryResolveDemoCost(true, input, out var cost, out var error));
            Assert.Equal(0m, cost);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("5000")]
        public void DemoCost_AnsweredNo_IsAlwaysZero(string? input)
        {
            Assert.True(VideoSponsorPricing.TryResolveDemoCost(false, input, out var cost, out _));
            Assert.Equal(0m, cost);
        }

        // ---- Discount: highest held tier wins, never stacked, max 20% ----
        [Theory]
        [InlineData(false, false, false, 0)]
        [InlineData(false, false, true, 10)]
        [InlineData(false, true, false, 15)]
        [InlineData(true, false, false, 20)]
        [InlineData(false, true, true, 15)]
        [InlineData(true, true, true, 20)]
        public void VideoPerk_UsesHighestHeldTier(bool main, bool category, bool subcategory, decimal expected)
        {
            var (percent, _) = SponsorshipDiscountHelper.ResolveCrossTierPerk(SponsorshipType.Unknown, main, category, subcategory);

            Assert.Equal(expected, percent);
            Assert.True(percent <= 20m);
        }

        // ---- Eligibility: strict, no past-sponsor exception ----
        [Fact]
        public void Eligibility_VerifiedAndOldEnough_CanSponsor()
        {
            var (can, reasons) = VideoSponsorEligibility.Check(Entry(DirectoryStatus.Verified, daysListed: 200), Now);

            Assert.True(can);
            Assert.Empty(reasons);
        }

        [Fact]
        public void Eligibility_NotVerified_CannotSponsor()
        {
            var (can, reasons) = VideoSponsorEligibility.Check(Entry(DirectoryStatus.Admitted, daysListed: 400), Now);

            Assert.False(can);
            Assert.Single(reasons);
        }

        [Fact]
        public void Eligibility_TooNew_CannotSponsor()
        {
            var (can, reasons) = VideoSponsorEligibility.Check(Entry(DirectoryStatus.Verified, daysListed: 179), Now);

            Assert.False(can);
            Assert.Single(reasons);
        }

        [Fact]
        public void Eligibility_ExactlyMinimumDays_CanSponsor()
        {
            var (can, _) = VideoSponsorEligibility.Check(Entry(DirectoryStatus.Verified, daysListed: 180), Now);

            Assert.True(can);
        }

        [Fact]
        public void Eligibility_SubcategoryWithSponsorshipDisabled_CannotSponsor()
        {
            var entry = Entry(DirectoryStatus.Verified, daysListed: 400);
            entry.SubCategory = new Subcategory { SponsorshipDisabled = true };

            var (can, reasons) = VideoSponsorEligibility.Check(entry, Now);

            Assert.False(can);
            Assert.Single(reasons);
        }

        // ---- BTCPay invoice status mapping ----
        [Theory]
        [InlineData("Settled", "", PaymentStatus.Paid)]
        [InlineData("Settled", "PaidOver", PaymentStatus.Paid)]
        [InlineData("Complete", "", PaymentStatus.Paid)]
        [InlineData("Processing", "", PaymentStatus.Pending)]
        [InlineData("Expired", "", PaymentStatus.Expired)]
        [InlineData("Expired", "PaidPartial", PaymentStatus.UnderPayment)]
        [InlineData("Invalid", "", PaymentStatus.Failed)]
        [InlineData("New", "", PaymentStatus.Unknown)]
        [InlineData(null, null, PaymentStatus.Unknown)]
        public void MapProcessorInvoiceStatus_Maps(string? status, string? additional, PaymentStatus expected)
        {
            Assert.Equal(expected, VideoSponsorOrderProcessor.MapProcessorInvoiceStatus(status, additional));
        }

        private static DirectoryEntry Entry(DirectoryStatus status, int daysListed) => new ()
        {
            Name = "Example",
            DirectoryEntryKey = "example",
            DirectoryStatus = status,
            CreateDate = Now.AddDays(-daysListed),
        };
    }
}
