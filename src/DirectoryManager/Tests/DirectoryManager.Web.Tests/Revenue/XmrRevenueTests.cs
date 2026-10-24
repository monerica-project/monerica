using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Tests.Revenue
{
    /// <summary>
    /// Admin revenue in XMR = the XMR actually paid (no price conversion).
    /// </summary>
    public class XmrRevenueTests
    {
        private static readonly DateTime Now = new (2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Sponsorship_UsesXmrActuallyPaid()
        {
            var inv = Invoice(usd: 300m, paidXmr: 0.75m);

            var converted = XmrRevenueConverter.ConvertInvoices(new[] { inv }).Single();

            Assert.Equal(0.75m, converted.Amount);
            Assert.Equal(300m, inv.Amount); // original untouched
        }

        [Fact]
        public void Sponsorship_NotPaidInXmr_CountsZeroAndIsReported()
        {
            var inv = Invoice(usd: 100m, paidXmr: 0m);

            Assert.Equal(0m, XmrRevenueConverter.ConvertInvoices(new[] { inv }).Single().Amount);
            var (count, usd) = XmrRevenueConverter.NotPaidInXmr(new[] { inv }, Array.Empty<RevenueItem>(), Now.AddDays(-30), Now.AddDays(1));
            Assert.Equal(1, count);
            Assert.Equal(100m, usd);
        }

        [Fact]
        public void Donation_UsesRecordedXmr_AffiliateWithoutXmrIsLeftOut()
        {
            var items = new[]
            {
                new RevenueItem { Source = RevenueSource.SubmissionReviews, PaidUtc = Now, AmountUsd = 5m, AmountXmr = 0.0123m },
                new RevenueItem { Source = RevenueSource.AffiliateCommissions, PaidUtc = Now, AmountUsd = 40m },
            };

            var xmr = XmrRevenueConverter.ConvertItems(items);
            Assert.Equal(0.0123m, xmr[0].AmountUsd);
            Assert.Equal(0m, xmr[1].AmountUsd);

            var (count, usd) = XmrRevenueConverter.NotPaidInXmr(Array.Empty<SponsoredListingInvoice>(), items, Now.AddDays(-1), Now.AddDays(1));
            Assert.Equal(1, count);
            Assert.Equal(40m, usd);
        }

        [Fact]
        public void Report_InXmr_KeepsSmallAmounts()
        {
            var items = XmrRevenueConverter.ConvertItems(new[]
            {
                new RevenueItem { Source = RevenueSource.VerificationReviews, PaidUtc = Now, AmountUsd = 1m, AmountXmr = 0.00191234m },
            });

            var report = RevenueReportBuilder.Build(
                Array.Empty<SponsoredListingInvoice>(), items, Now.AddDays(-1), Now.AddDays(1), RevenueBasis.Cash, Now, decimals: 8);

            Assert.Equal(0.00191234m, report.GrandTotal);
        }

        [Theory]
        [InlineData(1234.5, false, false, "$1,235")]
        [InlineData(1234.5, false, true, "$1,234.50")]
        [InlineData(2.05123, true, false, "2.05 XMR")]
        [InlineData(2.05123, true, true, "2.0512 XMR")]
        public void Money_FormatsInTheSelectedUnit(double amount, bool xmr, bool precise, string expected)
        {
            Assert.Equal(expected, RevenueMoney.Format((decimal)amount, xmr, precise));
        }

        private static SponsoredListingInvoice Invoice(decimal usd, decimal paidXmr) => new ()
        {
            CampaignStartDate = Now.AddDays(-10),
            CampaignEndDate = Now.AddDays(20),
            CreateDate = Now.AddDays(-10),
            Amount = usd,
            Currency = Currency.USD,
            PaidAmount = paidXmr,
            PaidInCurrency = paidXmr > 0 ? Currency.XMR : Currency.Unknown,
            PaymentStatus = PaymentStatus.Paid,
        };
    }
}
