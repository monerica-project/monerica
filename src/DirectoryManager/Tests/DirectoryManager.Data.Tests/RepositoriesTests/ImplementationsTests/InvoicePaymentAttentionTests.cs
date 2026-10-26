using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // The admin home page flags sponsor checkouts stuck between "created" and "paid".
    public class InvoicePaymentAttentionTests
    {
        [Fact]
        public void CountsPendingUnderpaidAndRecentOverpaid()
        {
            using var context = Seeded();
            var repo = new SponsoredListingInvoiceRepository(context);

            var (pending, underPaid, overPaid) = repo.GetPaymentAttentionCounts();

            Assert.Equal(1, pending);
            Assert.Equal(2, underPaid);
            Assert.Equal(1, overPaid); // the 60-day-old overpayment is no longer flagged
        }

        [Fact]
        public void UnderpaidCheckout_ShowsAsNewestWaiting()
        {
            using var context = Seeded();
            var repo = new SponsoredListingInvoiceRepository(context);

            Assert.Equal(Now.AddHours(-1), repo.GetLastPendingInvoiceCreateDate());
        }

        [Fact]
        public void Overpaid_CountsAsLastPaid()
        {
            using var context = Seeded();
            var repo = new SponsoredListingInvoiceRepository(context);

            Assert.Equal(Now.AddDays(-2), repo.GetLastPaidInvoiceCreateDate());
        }

        private static readonly DateTime Now = DateTime.UtcNow;

        private static SponsoredListingInvoice Inv(int id, PaymentStatus status, DateTime created) => new SponsoredListingInvoice
        {
            SponsoredListingInvoiceId = id,
            InvoiceId = Guid.NewGuid(),
            PaymentStatus = status,
            CreateDate = created,
        };

        private static ApplicationDbContext Seeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"inv-{Guid.NewGuid():N}")
                .Options;
            var context = new ApplicationDbContext(options);
            context.SponsoredListingInvoices.AddRange(
                Inv(1, PaymentStatus.Paid, Now.AddDays(-5)),
                Inv(2, PaymentStatus.OverPayment, Now.AddDays(-2)),
                Inv(3, PaymentStatus.OverPayment, Now.AddDays(-60)),
                Inv(4, PaymentStatus.Pending, Now.AddDays(-3)),
                Inv(5, PaymentStatus.UnderPayment, Now.AddHours(-1)),
                Inv(6, PaymentStatus.UnderPayment, Now.AddDays(-10)),
                Inv(7, PaymentStatus.InvoiceCreated, Now),
                Inv(8, PaymentStatus.Expired, Now));
            context.SaveChanges();
            return context;
        }
    }
}
