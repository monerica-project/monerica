using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Services.Implementations;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class RevenueOpportunityServiceTests
    {
        [Theory]
        [InlineData(PaymentStatus.InvoiceCreated, true)]
        [InlineData(PaymentStatus.Pending, true)]
        [InlineData(PaymentStatus.UnderPayment, true)]
        [InlineData(PaymentStatus.Expired, true)]
        [InlineData(PaymentStatus.Paid, false)]
        [InlineData(PaymentStatus.OverPayment, false)]
        [InlineData(PaymentStatus.Refunded, false)]
        [InlineData(PaymentStatus.Test, false)]
        public void Unfinished_MeansStartedButNotPaid(PaymentStatus status, bool expected)
            => Assert.Equal(expected, RevenueOpportunityService.IsUnfinished(status));

        [Fact]
        public void Scope_IsTheSpotBeingBought()
        {
            Assert.Null(RevenueOpportunityService.ScopeId(SponsorshipType.MainSponsor, 5, 75));
            Assert.Equal(5, RevenueOpportunityService.ScopeId(SponsorshipType.CategorySponsor, 5, 75));
            Assert.Equal(75, RevenueOpportunityService.ScopeId(SponsorshipType.SubcategorySponsor, 5, 75));
        }
    }
}
