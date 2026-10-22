using System.Reflection;
using System.Text;
using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    // End-to-end tests of the sponsorship purchase: the buyer confirms checkout, a BTCPay invoice
    // is created, BTCPay's webhooks report the payment, the sponsored listing goes live, and the
    // success page shows it as paid. Real controller + real repositories over an in-memory
    // database; only BTCPay itself is faked.
    public class SponsorshipPaymentFlowTests
    {
        private const string CaptchaContextCheckout = "sponsoredlisting-confirmcheckout";
        private const int EntryId = 10;
        private const int SubCategoryId = 2;
        private const int OfferId = 5;

        [Fact]
        public async Task Checkout_ThenSettledWebhook_ActivatesListing_AndSuccessPageShowsPaid()
        {
            using var h = new Harness();

            // 1. Buyer confirms checkout (no-JS BTCPay flow, the default).
            var confirm = await h.ConfirmCheckoutAsync(email: "buyer@example.com");
            var toInvoicePage = Assert.IsType<RedirectToActionResult>(confirm);
            Assert.Equal("BtcPayNoJsInvoice", toInvoicePage.ActionName);
            Assert.Equal(h.BtcPay.LastCreatedId, toInvoicePage.RouteValues!["processorInvoiceId"]);

            var invoice = await h.Db.SponsoredListingInvoices.AsNoTracking().SingleAsync();
            Assert.Equal(PaymentStatus.InvoiceCreated, invoice.PaymentStatus);
            Assert.Equal(PaymentProcessor.BTCPayServer, invoice.PaymentProcessor);
            Assert.Equal(h.BtcPay.LastCreatedId, invoice.ProcessorInvoiceId);
            Assert.Equal("buyer@example.com", invoice.Email);
            Assert.Equal(300m, invoice.Amount);
            Assert.Null(invoice.SponsoredListingId);
            Assert.Empty(h.Db.SponsoredListings);

            // 2. Money arrives (unconfirmed), then confirms.
            Assert.IsType<OkResult>(await h.WebhookAsync("InvoiceReceivedPayment", invoice.ProcessorInvoiceId!, paymentValue: "1.5"));
            Assert.Equal(PaymentStatus.Pending, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Empty(h.Db.SponsoredListings);

            Assert.IsType<OkResult>(await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!));

            // 3. The listing is live and linked both ways.
            invoice = await h.ReloadInvoiceAsync();
            Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
            var listing = await h.Db.SponsoredListings.AsNoTracking().SingleAsync();
            Assert.Equal(EntryId, listing.DirectoryEntryId);
            Assert.Equal(SponsorshipType.SubcategorySponsor, listing.SponsorshipType);
            Assert.Equal(SubCategoryId, listing.SubCategoryId);
            Assert.Equal(invoice.SponsoredListingInvoiceId, listing.SponsoredListingInvoiceId);
            Assert.Equal(listing.SponsoredListingId, invoice.SponsoredListingId);
            Assert.Equal(30, (int)Math.Round((listing.CampaignEndDate - listing.CampaignStartDate).TotalDays));
            Assert.Equal(1.5m, invoice.PaidAmount);
            Assert.True(await h.SponsoredListings.IsSponsoredListingActive(EntryId, SponsorshipType.SubcategorySponsor));

            // 4. The success page shows it as paid, not pending.
            var success = Assert.IsType<ViewResult>(await h.Controller.BtcPaySuccess(invoice.InvoiceId));
            Assert.Equal("BtcPaySuccess", success.ViewName);
            Assert.False((bool)success.ViewData["IsPending"]!);
            Assert.Equal(invoice.InvoiceId, Assert.IsType<SuccessViewModel>(success.Model).OrderId);

            // 5. BTCPay redelivers the same event: still exactly one listing.
            Assert.IsType<OkResult>(await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!));
            Assert.Single(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task Confirm_SkipsCaptcha_AfterTheSelectDurationCaptcha_OncePerSolve()
        {
            using var h = new Harness();

            // Solved the select-duration captcha in this session: confirm needs no captcha.
            var first = await h.ConfirmCheckoutAsync(solveCaptcha: false, verifiedEarlier: true);
            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(first).ActionName);

            // That solve is used up: a second invoice in the same session needs the captcha again.
            // (The wrong-captcha path re-renders the confirm page, which needs the cache service the
            // harness leaves out, so it throws here; what matters is that no second invoice exists.)
            await Assert.ThrowsAnyAsync<Exception>(() => h.ConfirmCheckoutAsync(solveCaptcha: false, keepSession: true));
            Assert.Single(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task SuccessPage_BeforeWebhook_ShowsPending()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            var success = Assert.IsType<ViewResult>(await h.Controller.BtcPaySuccess(invoice.InvoiceId));
            Assert.True((bool)success.ViewData["IsPending"]!);
        }

        [Fact]
        public async Task Renewal_ExtendsTheCurrentSponsorship_InsteadOfAddingAnother()
        {
            using var h = new Harness();
            var currentEnd = DateTime.UtcNow.AddDays(5);
            h.Db.SponsoredListings.Add(new SponsoredListing
            {
                DirectoryEntryId = EntryId,
                SponsorshipType = SponsorshipType.SubcategorySponsor,
                SubCategoryId = SubCategoryId,
                CampaignStartDate = DateTime.UtcNow.AddDays(-25),
                CampaignEndDate = currentEnd,
                SponsoredListingInvoiceId = 999,
            });
            await h.Db.SaveChangesAsync();

            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            Assert.Equal(currentEnd, invoice.CampaignStartDate, TimeSpan.FromSeconds(1));

            await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!);

            var listing = await h.Db.SponsoredListings.AsNoTracking().SingleAsync();
            Assert.Equal(currentEnd.AddDays(30), listing.CampaignEndDate, TimeSpan.FromSeconds(1));
            Assert.Equal(listing.SponsoredListingId, (await h.ReloadInvoiceAsync()).SponsoredListingId);
        }

        // A sponsor whose listing MOVED subcategory still has a live campaign in the old one. That is
        // not an extension of the new subcategory's slot: if another listing holds that slot, the
        // buyer is sent to the waiting page instead of getting an invoice.
        [Fact]
        public async Task MovedListing_CannotBuyANewSubcategorySlotThatIsTaken()
        {
            using var h = new Harness();
            h.Db.DirectoryEntries.Add(new DirectoryEntry
            {
                DirectoryEntryId = 11,
                Name = "Other Swap",
                DirectoryEntryKey = "other-swap",
                Link = "https://other.example",
                SubCategoryId = SubCategoryId,
                DirectoryStatus = DirectoryStatus.Verified,
            });
            h.Db.SponsoredListings.AddRange(
                new SponsoredListing
                {
                    DirectoryEntryId = EntryId,
                    SponsorshipType = SponsorshipType.SubcategorySponsor,
                    SubCategoryId = 99, // the subcategory the listing moved out of
                    CampaignStartDate = DateTime.UtcNow.AddDays(-25),
                    CampaignEndDate = DateTime.UtcNow.AddDays(5),
                    SponsoredListingInvoiceId = 998,
                },
                new SponsoredListing
                {
                    DirectoryEntryId = 11,
                    SponsorshipType = SponsorshipType.SubcategorySponsor,
                    SubCategoryId = SubCategoryId, // the new subcategory's only slot, held by someone else
                    CampaignStartDate = DateTime.UtcNow.AddDays(-10),
                    CampaignEndDate = DateTime.UtcNow.AddDays(20),
                    SponsoredListingInvoiceId = 997,
                });
            await h.Db.SaveChangesAsync();

            Assert.False(await h.SponsoredListings.IsSponsoredListingActiveInScopeAsync(EntryId, SponsorshipType.SubcategorySponsor, SubCategoryId));
            Assert.True(await h.SponsoredListings.IsSponsoredListingActiveInScopeAsync(EntryId, SponsorshipType.SubcategorySponsor, 99));

            var result = await h.ConfirmCheckoutAsync();

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Contains("/sponsoredlisting/checkout-waiting", redirect.Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
        }

        // When the new subcategory's slot is free, the moved listing buys it as a NEW campaign: it
        // starts now (not when the old campaign ends) and paying it creates a separate listing
        // instead of extending the old subcategory's one.
        [Fact]
        public async Task MovedListing_BuysTheFreeSlotAsANewCampaign_OldOneUntouched()
        {
            using var h = new Harness();
            var oldEnd = DateTime.UtcNow.AddDays(5);
            h.Db.SponsoredListings.Add(new SponsoredListing
            {
                DirectoryEntryId = EntryId,
                SponsorshipType = SponsorshipType.SubcategorySponsor,
                SubCategoryId = 99,
                CampaignStartDate = DateTime.UtcNow.AddDays(-25),
                CampaignEndDate = oldEnd,
                SponsoredListingInvoiceId = 998,
            });
            await h.Db.SaveChangesAsync();

            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            Assert.True(invoice.CampaignStartDate < DateTime.UtcNow.AddMinutes(1), "a new slot starts now, not after the old campaign");

            await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!);

            var listings = await h.Db.SponsoredListings.AsNoTracking().ToListAsync();
            Assert.Equal(2, listings.Count);
            var old = listings.Single(l => l.SubCategoryId == 99);
            Assert.Equal(oldEnd, old.CampaignEndDate, TimeSpan.FromSeconds(1));
            var created = listings.Single(l => l.SubCategoryId == SubCategoryId);
            Assert.Equal(created.SponsoredListingId, (await h.ReloadInvoiceAsync()).SponsoredListingId);
        }

        [Fact]
        public async Task ReconfirmCancelsOldInvoice_ButPayingItStillActivatesTheListing()
        {
            using var h = new Harness();

            // Buyer reaches the invoice, goes back, confirms again with the same reservation.
            var rsvId = await h.ReserveAsync();
            await h.ConfirmCheckoutAsync(rsvId: rsvId);
            var first = await h.ReloadInvoiceAsync();
            await h.ConfirmCheckoutAsync(rsvId: rsvId);

            first = await h.Db.SponsoredListingInvoices.AsNoTracking().SingleAsync(i => i.SponsoredListingInvoiceId == first.SponsoredListingInvoiceId);
            Assert.Equal(PaymentStatus.Canceled, first.PaymentStatus);

            // ...but their wallet pays the FIRST invoice's address.
            Assert.IsType<OkResult>(await h.WebhookAsync("InvoiceSettled", first.ProcessorInvoiceId!));

            first = await h.Db.SponsoredListingInvoices.AsNoTracking().SingleAsync(i => i.SponsoredListingInvoiceId == first.SponsoredListingInvoiceId);
            Assert.Equal(PaymentStatus.Paid, first.PaymentStatus);
            var listing = await h.Db.SponsoredListings.AsNoTracking().SingleAsync();
            Assert.Equal(first.SponsoredListingInvoiceId, listing.SponsoredListingInvoiceId);
        }

        [Fact]
        public async Task ListingCreateFails_WebhookErrors_AndTheRetryActivates()
        {
            using var h = new Harness(failFirstListingCreate: true);
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            // First delivery: the invoice is saved as Paid, then creating the listing blows up.
            // The exception becomes a 500, so BTCPay redelivers.
            await Assert.ThrowsAsync<InvalidOperationException>(() => h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!));
            invoice = await h.ReloadInvoiceAsync();
            Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
            Assert.Null(invoice.SponsoredListingId);
            Assert.Empty(h.Db.SponsoredListings);

            // Redelivery: the paid-but-not-activated invoice is finished, not skipped.
            Assert.IsType<OkResult>(await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!));
            invoice = await h.ReloadInvoiceAsync();
            var listing = await h.Db.SponsoredListings.AsNoTracking().SingleAsync();
            Assert.Equal(listing.SponsoredListingId, invoice.SponsoredListingId);
        }

        [Fact]
        public async Task AdminMarksPaid_ActivatesTheListing()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            invoice.PaymentStatus = PaymentStatus.Paid;
            await h.Activator.SaveAndActivateAsync(invoice);

            var listing = await h.Db.SponsoredListings.AsNoTracking().SingleAsync();
            Assert.Equal(listing.SponsoredListingId, (await h.ReloadInvoiceAsync()).SponsoredListingId);
        }

        [Fact]
        public async Task OldPaidInvoice_WhoseCampaignEnded_DoesNotCreateAListing()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            invoice.PaymentStatus = PaymentStatus.Paid;
            invoice.CampaignStartDate = DateTime.UtcNow.AddDays(-400);
            invoice.CampaignEndDate = DateTime.UtcNow.AddDays(-370);
            await h.Activator.SaveAndActivateAsync(invoice);

            Assert.Empty(h.Db.SponsoredListings);
            Assert.Equal(PaymentStatus.Paid, (await h.ReloadInvoiceAsync()).PaymentStatus);
        }

        [Fact]
        public async Task FinalNonPaidStatus_IgnoresLaterEvents()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            await h.WebhookAsync("InvoiceInvalid", invoice.ProcessorInvoiceId!);
            Assert.Equal(PaymentStatus.Failed, (await h.ReloadInvoiceAsync()).PaymentStatus);

            await h.WebhookAsync("InvoiceProcessing", invoice.ProcessorInvoiceId!);
            Assert.Equal(PaymentStatus.Failed, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Empty(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task ExpiredInvoice_ThatIsThenSettled_Activates()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);
            Assert.Equal(PaymentStatus.Expired, (await h.ReloadInvoiceAsync()).PaymentStatus);

            await h.WebhookAsync("InvoiceSettled", invoice.ProcessorInvoiceId!);
            Assert.Equal(PaymentStatus.Paid, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Single(h.Db.SponsoredListings);
        }

        [Theory]
        [InlineData(PaymentStatus.InvoiceCreated, PaymentStatus.Pending, true)]
        [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, true)]
        [InlineData(PaymentStatus.Paid, PaymentStatus.Pending, false)]
        [InlineData(PaymentStatus.Paid, PaymentStatus.Paid, false)]
        [InlineData(PaymentStatus.Canceled, PaymentStatus.Paid, true)]
        [InlineData(PaymentStatus.Expired, PaymentStatus.Paid, true)]
        [InlineData(PaymentStatus.Canceled, PaymentStatus.Pending, false)]
        [InlineData(PaymentStatus.Failed, PaymentStatus.Paid, false)]
        [InlineData(PaymentStatus.Refunded, PaymentStatus.Paid, false)]
        [InlineData(PaymentStatus.Pending, PaymentStatus.Unknown, false)]
        [InlineData(PaymentStatus.Pending, PaymentStatus.UnderPayment, true)]   // expired with a partial payment
        [InlineData(PaymentStatus.UnderPayment, PaymentStatus.Paid, true)]      // topped up afterwards
        [InlineData(PaymentStatus.Paid, PaymentStatus.UnderPayment, false)]
        public void CanAdvanceStatus_OnlyForwardExceptPaymentAfterLocalCancel(PaymentStatus current, PaymentStatus proposed, bool expected)
        {
            Assert.Equal(expected, SponsoredListingCheckoutHelper.CanAdvanceStatus(current, proposed));
        }

        private sealed class Harness : IDisposable
        {
            private readonly ServiceProvider services;
            private readonly MemoryCache cache = new (new MemoryCacheOptions());

            public Harness(bool failFirstListingCreate = false)
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"sponsorship-flow-{Guid.NewGuid()}")
                    .Options;
                this.Db = new ApplicationDbContext(options);
                this.Seed();

                var listings = new SponsoredListingRepository(this.Db);
                this.SponsoredListings = failFirstListingCreate
                    ? FailOnceProxy<ISponsoredListingRepository>.Wrap(listings, nameof(ISponsoredListingRepository.CreateAsync))
                    : listings;
                var invoices = new SponsoredListingInvoiceRepository(this.Db);
                this.Activator = new SponsoredListingActivator(invoices, this.SponsoredListings, this.cache, NullLogger<SponsoredListingActivator>.Instance);

                this.services = new ServiceCollection().BuildServiceProvider();

                this.Controller = new SponsoredListingCheckoutController(
                    new SubcategoryRepository(this.Db),
                    new CategoryRepository(this.Db),
                    new DirectoryEntryRepository(this.Db, new DirectoryEntriesAuditRepository(this.Db)),
                    this.SponsoredListings,
                    invoices,
                    null!, // NOWPayments: not offered
                    this.BtcPay,
                    this.cache,
                    new SponsoredListingOfferRepository(this.Db),
                    new SponsoredListingReservationRepository(this.Db),
                    null!, // cache service: only for confirm-page error re-renders
                    this.Activator,
                    NullLogger<SponsoredListingCheckoutController>.Instance);
                this.NewRequest();
            }

            public ApplicationDbContext Db { get; }

            public ISponsoredListingRepository SponsoredListings { get; }

            public SponsoredListingActivator Activator { get; }

            public FakeBtcPay BtcPay { get; } = new ();

            public SponsoredListingCheckoutController Controller { get; }

            public async Task<IActionResult> ConfirmCheckoutAsync(string? email = null, Guid? rsvId = null, bool solveCaptcha = true, bool verifiedEarlier = false, bool keepSession = false)
            {
                var session = keepSession ? this.Controller.HttpContext.Session : null;
                this.NewRequest();
                if (session != null)
                {
                    this.Controller.HttpContext.Session = session;
                }

                if (solveCaptcha)
                {
                    CaptchaTools.Store(this.Controller.HttpContext, CaptchaContextCheckout, "ABCDE");
                }

                if (verifiedEarlier)
                {
                    this.Controller.HttpContext.Session.SetString(
                        $"sponsoredlisting-verified:{EntryId}",
                        DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                this.Controller.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
                this.Controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    ["Captcha"] = "ABCDE",
                });

                return await this.Controller.ConfirmedCheckoutAsync(EntryId, OfferId, "BTCPayServerNoJs", rsvId, email);
            }

            public async Task<Guid> ReserveAsync()
            {
                var group = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.SubcategorySponsor, SubCategoryId);
                var reservation = await new SponsoredListingReservationRepository(this.Db)
                    .CreateReservationAsync(DateTime.UtcNow.AddMinutes(15), group, $"entry {EntryId}");
                return reservation.ReservationGuid;
            }

            public async Task<IActionResult> WebhookAsync(string type, string processorInvoiceId, string? paymentValue = null)
            {
                this.NewRequest();
                var payload = new
                {
                    deliveryId = Guid.NewGuid().ToString(),
                    type,
                    invoiceId = processorInvoiceId,
                    payment = paymentValue == null ? null : new { value = paymentValue },
                };
                this.Controller.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
                this.Controller.HttpContext.Request.Headers["BTCPay-Sig"] = "sha256=test";

                // Each webhook is a separate request with a fresh change tracker, as in production.
                this.Db.ChangeTracker.Clear();
                return await this.Controller.BtcPayCallbackAsync();
            }

            public async Task<SponsoredListingInvoice> ReloadInvoiceAsync()
            {
                this.Db.ChangeTracker.Clear();
                return await this.Db.SponsoredListingInvoices.AsNoTracking()
                    .OrderByDescending(i => i.SponsoredListingInvoiceId).FirstAsync();
            }

            public void Dispose()
            {
                this.Db.Dispose();
                this.cache.Dispose();
                this.services.Dispose();
            }

            private void NewRequest()
            {
                var http = new DefaultHttpContext { RequestServices = this.services };
                http.Session = new TestSession();
                this.Controller.ControllerContext = new ControllerContext { HttpContext = http };
                this.Controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
                this.Controller.Url = new StubUrlHelper(this.Controller.ControllerContext);
            }

            private void Seed()
            {
                var category = new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges" };
                var sub = new Subcategory { SubCategoryId = SubCategoryId, Name = "Instant Swaps", SubCategoryKey = "instant-swaps", Category = category, CategoryId = 1 };
                this.Db.Categories.Add(category);
                this.Db.Subcategories.Add(sub);
                this.Db.DirectoryEntries.Add(new DirectoryEntry
                {
                    DirectoryEntryId = EntryId,
                    Name = "Example Swap",
                    DirectoryEntryKey = "example-swap",
                    Link = "https://example.com",
                    SubCategory = sub,
                    SubCategoryId = SubCategoryId,
                    DirectoryStatus = DirectoryStatus.Verified,

                    // Eligible to sponsor: Verified AND listed long enough (checkout re-checks this on every POST).
                    CreateDate = DateTime.UtcNow.AddDays(-365),
                });
                this.Db.SponsoredListingOffers.Add(new SponsoredListingOffer
                {
                    SponsoredListingOfferId = OfferId,
                    Description = "30 days subcategory sponsor",
                    Days = 30,
                    Price = 300m,
                    PriceCurrency = Currency.USD,
                    IsEnabled = true,
                    SponsorshipType = SponsorshipType.SubcategorySponsor,
                });
                this.Db.SaveChanges();
            }
        }

        private sealed class FakeBtcPay : IBtcPayServerService
        {
            private int counter;

            public string? LastCreatedId { get; private set; }

            public string DefaultCurrency => "USD";

            public string SuccessUrl => "https://example.test/sponsoredlisting/btcpaysuccess";

            public string CancelUrl => "https://example.test/cancel";

            public string ReviewDonationsStoreId => string.Empty;

            public string BaseUrl => "https://btcpay.example.test";

            public Task<BtcPayInvoiceResponse> CreateInvoiceAsync(BtcPayInvoiceRequest request)
            {
                this.LastCreatedId = $"inv{++this.counter}";
                return Task.FromResult(new BtcPayInvoiceResponse { Id = this.LastCreatedId, Status = "New", CheckoutLink = $"https://btcpay.example.test/i/{this.LastCreatedId}" });
            }

            public Task<BtcPayInvoiceResponse> GetInvoiceAsync(string invoiceId) =>
                Task.FromResult(new BtcPayInvoiceResponse { Id = invoiceId, Status = "New" });

            public Task<BtcPayInvoiceResponse> CreateInvoiceOnStoreAsync(string storeId, BtcPayInvoiceRequest request) => throw new NotSupportedException();

            public Task<BtcPayInvoiceResponse> GetInvoiceOnStoreAsync(string storeId, string invoiceId) => throw new NotSupportedException();

            public Task<BtcPayPaymentMethod?> GetXmrPaymentMethodOnStoreAsync(string storeId, string invoiceId) => throw new NotSupportedException();

            public Task<BtcPayPaymentMethod?> GetXmrPaymentMethodAsync(string invoiceId) =>
                Task.FromResult<BtcPayPaymentMethod?>(new BtcPayPaymentMethod { Rate = "200", PaymentMethodPaid = "1.5", TotalPaid = "1.5", Amount = "1.5" });

            public Task<BtcPayStoreRateResponse?> GetStoreRateAsync(string baseCurrency, string quoteCurrency) => Task.FromResult<BtcPayStoreRateResponse?>(null);

            public Task<decimal> GetXmrRateAsync(string quoteCurrency = "USD") => Task.FromResult(200m);

            public bool IsWebhookValid(string requestBody, string btcPaySigHeader, out string errorMsg)
            {
                errorMsg = string.Empty;
                return true;
            }
        }

        // Wraps a repository so one named method throws on its first call only (simulates a
        // transient database failure part-way through activation).
        private class FailOnceProxy<T> : DispatchProxy
            where T : class
        {
            private T inner = null!;
            private string method = string.Empty;
            private bool failed;

            public static T Wrap(T inner, string method)
            {
                var proxy = Create<T, FailOnceProxy<T>>();
                var self = (FailOnceProxy<T>)(object)proxy;
                self.inner = inner;
                self.method = method;
                return proxy;
            }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (!this.failed && targetMethod!.Name == this.method)
                {
                    this.failed = true;
                    throw new InvalidOperationException("Simulated database failure.");
                }

                try
                {
                    return targetMethod!.Invoke(this.inner, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }
            }
        }

        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> store = new ();

            public bool IsAvailable => true;

            public string Id { get; } = Guid.NewGuid().ToString();

            public IEnumerable<string> Keys => this.store.Keys;

            public void Clear() => this.store.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Remove(string key) => this.store.Remove(key);

            public void Set(string key, byte[] value) => this.store[key] = value;

            public bool TryGetValue(string key, out byte[] value) => this.store.TryGetValue(key, out value!);
        }

        private sealed class StubUrlHelper : IUrlHelper
        {
            public StubUrlHelper(ActionContext context) => this.ActionContext = context;

            public ActionContext ActionContext { get; }

            public string? Action(UrlActionContext actionContext) => "/" + actionContext.Action;

            public string? Content(string? contentPath) => contentPath;

            public bool IsLocalUrl(string? url) => true;

            public string? Link(string? routeName, object? values) => "/";

            public string? RouteUrl(UrlRouteContext routeContext) => "/";
        }

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }
    }
}
