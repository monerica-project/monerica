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
using DirectoryManager.Web.Services.Interfaces;
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
        private const int MainOfferId = 30;

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
        public async Task CategoryCheckout_ChargesTheCategoryPagePrice_AndRejectsTheStandardPrice()
        {
            using var h = new Harness();
            h.Db.SponsoredListingOffers.AddRange(
                new SponsoredListingOffer { SponsoredListingOfferId = 20, Description = "standard", Days = 30, Price = 100m, PriceCurrency = Currency.USD, IsEnabled = true, SponsorshipType = SponsorshipType.CategorySponsor },
                new SponsoredListingOffer { SponsoredListingOfferId = 21, Description = "exchanges page", Days = 30, Price = 600m, PriceCurrency = Currency.USD, IsEnabled = true, SponsorshipType = SponsorshipType.CategorySponsor, CategoryId = 1 });
            await h.Db.SaveChangesAsync();

            // The cheaper standard price is not offered for a category page that has its own price.
            var cheap = await h.ConfirmCheckoutAsync(email: "buyer@example.com", offerId: 20);
            Assert.False(cheap is RedirectToActionResult r && r.ActionName == "BtcPayNoJsInvoice");
            Assert.Empty(h.Db.SponsoredListingInvoices);

            var confirm = await h.ConfirmCheckoutAsync(email: "buyer@example.com", offerId: 21);
            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(confirm).ActionName);

            var invoice = await h.Db.SponsoredListingInvoices.AsNoTracking().SingleAsync();
            Assert.Equal(600m, invoice.Amount);
            Assert.Equal(SponsorshipType.CategorySponsor, invoice.SponsorshipType);
            Assert.Equal(1, invoice.CategoryId);
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

        // ---- Payments that arrive after the invoice timer ran out (BTCPay: "Expired" + "PaidLate") ----
        [Fact]
        public async Task LatePayment_ConfirmedInsideGrace_GoesLiveOnItsOwn()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: true);
            await h.WebhookAsync("InvoicePaymentSettled", invoice.ProcessorInvoiceId!);

            var after = await h.ReloadInvoiceAsync();
            Assert.Equal(PaymentStatus.Paid, after.PaymentStatus);
            Assert.Contains("LatePaymentAccepted", after.PaymentResponse);
            Assert.Equal(1.5m, after.PaidAmount);
            Assert.Single(h.Db.SponsoredListings);

            // The full paid period is delivered even though the listing started late.
            var listing = h.Db.SponsoredListings.Single();
            Assert.Equal(30, Math.Round((listing.CampaignEndDate - listing.CampaignStartDate).TotalDays));
            Assert.True(listing.CampaignEndDate > DateTime.UtcNow.AddDays(29.9));

            // A repeat of the event, or the 5-minute pass, changes nothing.
            await h.WebhookAsync("InvoicePaymentSettled", invoice.ProcessorInvoiceId!);
            Assert.Equal(LatePaymentOutcome.NotApplicable, await h.LatePayments.TryAcceptAsync(after.SponsoredListingInvoiceId));
            Assert.Single(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task LatePayment_NotYetConfirmed_Waits_ThenThePassAcceptsIt()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: false);
            await h.WebhookAsync("InvoiceReceivedPayment", invoice.ProcessorInvoiceId!);
            Assert.Equal(PaymentStatus.Expired, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Empty(h.Db.SponsoredListings);

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: true);
            h.Db.ChangeTracker.Clear();
            Assert.Equal(LatePaymentOutcome.Accepted, await h.LatePayments.TryAcceptAsync(invoice.SponsoredListingInvoiceId));
            Assert.Equal(PaymentStatus.Paid, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Single(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task LatePayment_AfterTheGracePeriod_IsLeftForTheOwner()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 180, settled: true);
            await h.WebhookAsync("InvoicePaymentSettled", invoice.ProcessorInvoiceId!);

            var after = await h.ReloadInvoiceAsync();
            Assert.Equal(PaymentStatus.Expired, after.PaymentStatus);
            Assert.Contains("LatePaymentTooLate", after.PaymentResponse);
            Assert.Empty(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task LatePayment_WhenTheSlotWasSoldMeanwhile_IsLeftForTheOwner()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            for (int i = 0; i < DirectoryManager.Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings; i++)
            {
                await h.AddEligibleEntryAsync(900 + i);
                h.Db.SponsoredListings.Add(new SponsoredListing
                {
                    DirectoryEntryId = 900 + i,
                    SponsorshipType = SponsorshipType.SubcategorySponsor,
                    SubCategoryId = SubCategoryId,
                    CampaignStartDate = DateTime.UtcNow.AddDays(-1),
                    CampaignEndDate = DateTime.UtcNow.AddDays(29),
                });
            }

            await h.Db.SaveChangesAsync();
            int before = h.Db.SponsoredListings.Count();

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: true);
            await h.WebhookAsync("InvoicePaymentSettled", invoice.ProcessorInvoiceId!);

            var after = await h.ReloadInvoiceAsync();
            Assert.Equal(PaymentStatus.Expired, after.PaymentStatus);
            Assert.Contains("LatePaymentSlotTaken", after.PaymentResponse);
            Assert.Equal(before, h.Db.SponsoredListings.Count());
        }

        [Fact]
        public async Task LatePayment_WhileAnotherBuyerIsCheckingOut_WaitsWithoutANote()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            await h.AddEligibleEntryAsync(901);
            for (int i = 0; i < DirectoryManager.Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings; i++)
            {
                await h.ReserveAsync(entryId: 901);
            }

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: true);
            h.Db.ChangeTracker.Clear();
            Assert.Equal(LatePaymentOutcome.SlotHeld, await h.LatePayments.TryAcceptAsync(invoice.SponsoredListingInvoiceId));
            Assert.Equal(PaymentStatus.Expired, (await h.ReloadInvoiceAsync()).PaymentStatus);
        }

        [Fact]
        public async Task ExpiredInvoice_PartlyPaidOrUnpaid_IsNeverAccepted()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();
            await h.WebhookAsync("InvoiceExpired", invoice.ProcessorInvoiceId!);

            h.BtcPay.SetPaidLate(minutesAfterExpiry: 1, settled: true, paid: "0.7");
            h.Db.ChangeTracker.Clear();
            Assert.Equal(LatePaymentOutcome.NotApplicable, await h.LatePayments.TryAcceptAsync(invoice.SponsoredListingInvoiceId));

            h.BtcPay.SetExpired("PaidPartial");
            Assert.Equal(LatePaymentOutcome.NotApplicable, await h.LatePayments.TryAcceptAsync(invoice.SponsoredListingInvoiceId));
            Assert.Equal(PaymentStatus.Expired, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Empty(h.Db.SponsoredListings);
        }

        [Fact]
        public async Task SettledAtBtcPay_ButWebhookNeverArrived_ThePassActivatesIt()
        {
            using var h = new Harness();
            await h.ConfirmCheckoutAsync();
            var invoice = await h.ReloadInvoiceAsync();

            h.BtcPay.SetSettled();
            h.Db.ChangeTracker.Clear();
            var accepted = await SponsorLatePaymentSweeper.SweepAsync(h.SweepServices(), DateTime.UtcNow, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(1, accepted);
            Assert.Equal(PaymentStatus.Paid, (await h.ReloadInvoiceAsync()).PaymentStatus);
            Assert.Single(h.Db.SponsoredListings);
        }

        // ---- Slot holds: an invoice only exists for a buyer holding (or just taking) a free slot ----
        [Fact]
        public async Task Confirm_WithoutRsvId_WhenTheSlotIsHeldBySomeoneElse_GoesToWaiting_NoInvoice()
        {
            using var h = new Harness();
            await h.ReserveAsync(entryId: 11); // the subcategory's only slot, held by another listing

            var result = await h.ConfirmCheckoutAsync();

            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(result).Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
            Assert.Single(h.Db.SponsoredListingReservations);
        }

        [Fact]
        public async Task Confirm_WithoutRsvId_WhenTheSlotIsFree_TakesAHold_AndTiesTheInvoiceToIt()
        {
            using var h = new Harness();

            var result = await h.ConfirmCheckoutAsync();

            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var hold = await h.Db.SponsoredListingReservations.AsNoTracking().SingleAsync();
            Assert.Equal(ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.SubcategorySponsor, SubCategoryId), hold.ReservationGroup);
            Assert.Contains($"ListingId={EntryId};", hold.Details);
            Assert.True(hold.ExpirationDateTime > DateTime.UtcNow);
            Assert.Equal(hold.ReservationGuid, (await h.ReloadInvoiceAsync()).ReservationGuid);

            // A second buyer (another listing) now finds the slot held.
            await h.AddEligibleEntryAsync(11);
            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(await h.ConfirmCheckoutAsync(entryId: 11)).Url);
            Assert.Single(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task Confirm_WithAnExpiredRsvId_TakesAFreshHold_WhenTheSlotIsFree()
        {
            using var h = new Harness();
            var expired = await h.ReserveAsync(minutes: -5);

            var result = await h.ConfirmCheckoutAsync(rsvId: expired);

            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var invoice = await h.ReloadInvoiceAsync();
            Assert.NotEqual(expired, invoice.ReservationGuid);
            var fresh = await h.Db.SponsoredListingReservations.AsNoTracking().SingleAsync(r => r.ReservationGuid == invoice.ReservationGuid);
            Assert.True(fresh.ExpirationDateTime > DateTime.UtcNow);
        }

        [Fact]
        public async Task Confirm_WithAnExpiredRsvId_GoesToWaiting_WhenSomeoneElseNowHoldsTheSlot()
        {
            using var h = new Harness();
            var expired = await h.ReserveAsync(minutes: -5);
            await h.ReserveAsync(entryId: 11);

            var result = await h.ConfirmCheckoutAsync(rsvId: expired);

            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(result).Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task Confirm_WithAnotherListingsRsvId_IsNotAcceptedAsAHold()
        {
            using var h = new Harness();
            var someoneElses = await h.ReserveAsync(entryId: 11);

            var result = await h.ConfirmCheckoutAsync(rsvId: someoneElses);

            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(result).Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task Confirm_WithOwnLiveRsvId_UsesThatHold()
        {
            using var h = new Harness();
            var mine = await h.ReserveAsync();

            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(await h.ConfirmCheckoutAsync(rsvId: mine)).ActionName);
            Assert.Equal(mine, (await h.ReloadInvoiceAsync()).ReservationGuid);
            Assert.Single(h.Db.SponsoredListingReservations);
        }

        [Fact]
        public async Task Renewal_IsAllowed_EvenWhenAnotherBuyerHoldsTheSlot()
        {
            using var h = new Harness();
            h.Db.SponsoredListings.Add(new SponsoredListing
            {
                DirectoryEntryId = EntryId,
                SponsorshipType = SponsorshipType.SubcategorySponsor,
                SubCategoryId = SubCategoryId,
                CampaignStartDate = DateTime.UtcNow.AddDays(-25),
                CampaignEndDate = DateTime.UtcNow.AddDays(5),
                SponsoredListingInvoiceId = 999,
            });
            await h.Db.SaveChangesAsync();
            await h.ReserveAsync(entryId: 11);

            var result = await h.ConfirmCheckoutAsync();

            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Single(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task MainSponsor_WithoutRsvId_TakesTheMainHoldAndTheSubcategoryShadowHold()
        {
            using var h = new Harness();
            await h.AddMainOfferAsync();

            var result = await h.ConfirmCheckoutAsync(offerId: MainOfferId);

            Assert.Equal("BtcPayNoJsInvoice", Assert.IsType<RedirectToActionResult>(result).ActionName);
            var holds = await h.Db.SponsoredListingReservations.AsNoTracking().ToListAsync();
            Assert.Equal(2, holds.Count);
            var main = holds.Single(r => r.ReservationGroup == ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, 0));
            var shadow = holds.Single(r => r.ReservationGroup == ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(SubCategoryId));
            Assert.Equal(main.ExpirationDateTime, shadow.ExpirationDateTime);
            Assert.Equal(main.ReservationGuid, (await h.ReloadInvoiceAsync()).ReservationGuid);
        }

        [Fact]
        public async Task MainSponsor_WithoutRsvId_GoesToWaiting_WhenAnotherBuyerHoldsTheSubcategoryShadow()
        {
            using var h = new Harness();
            await h.AddMainOfferAsync();
            await h.ReserveAsync(entryId: 11, group: ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(SubCategoryId));

            var result = await h.ConfirmCheckoutAsync(offerId: MainOfferId);

            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(result).Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task MainSponsor_WithOwnHold_GoesToWaiting_WhenOtherShadowsWouldOverfillTheSubcategory()
        {
            using var h = new Harness();
            await h.AddMainOfferAsync();
            var mainGroup = ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.MainSponsor, 0);
            var mine = await h.ReserveAsync(group: mainGroup);

            // Four main sponsors already live in this subcategory, and another buyer holds the shadow
            // for the fifth (our own shadow lapsed): we would be the sixth.
            for (var i = 0; i < 4; i++)
            {
                await h.AddEligibleEntryAsync(100 + i);
                h.Db.SponsoredListings.Add(new SponsoredListing
                {
                    DirectoryEntryId = 100 + i,
                    SponsorshipType = SponsorshipType.MainSponsor,
                    SubCategoryId = SubCategoryId,
                    CampaignStartDate = DateTime.UtcNow.AddDays(-1),
                    CampaignEndDate = DateTime.UtcNow.AddDays(20),
                    SponsoredListingInvoiceId = 900 + i,
                });
            }

            await h.Db.SaveChangesAsync();
            await h.ReserveAsync(entryId: 11, group: ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(SubCategoryId));

            var result = await h.ConfirmCheckoutAsync(rsvId: mine, offerId: MainOfferId);

            Assert.Contains("/sponsoredlisting/checkout-waiting", Assert.IsType<RedirectResult>(result).Url);
            Assert.Empty(h.Db.SponsoredListingInvoices);
        }

        [Fact]
        public async Task TryCreateReservationWithinCapacity_ConcurrentBuyers_OnlyOneGetsTheLastSlot()
        {
            var dbName = $"slot-race-{Guid.NewGuid()}";
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;
            const string group = "race-group";

            var attempts = Enumerable.Range(1, 20).Select(i => Task.Run(async () =>
            {
                using var db = new ApplicationDbContext(options);
                return await new SponsoredListingReservationRepository(db).TryCreateReservationWithinCapacityAsync(
                    DateTime.UtcNow.AddMinutes(15), group, $"Type=Test; ListingId={i}; ListingName=\"x\"", maxActiveReservations: 1);
            })).ToArray();
            var results = await Task.WhenAll(attempts);

            Assert.Single(results, r => r != null);
            using var check = new ApplicationDbContext(options);
            Assert.Equal(1, await check.SponsoredListingReservations.CountAsync());
        }

        [Fact]
        public async Task TryCreateReservationWithinCapacity_RefusesWhenNoRoom_AndIgnoresExpiredHolds()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"slot-cap-{Guid.NewGuid()}").Options;
            using var db = new ApplicationDbContext(options);
            var repo = new SponsoredListingReservationRepository(db);

            Assert.Null(await repo.TryCreateReservationWithinCapacityAsync(DateTime.UtcNow.AddMinutes(15), "g", "ListingId=1;", 0));

            await repo.CreateReservationAsync(DateTime.UtcNow.AddMinutes(-1), "g", "ListingId=2;");
            Assert.NotNull(await repo.TryCreateReservationWithinCapacityAsync(DateTime.UtcNow.AddMinutes(15), "g", "ListingId=1;", 1));
            Assert.Null(await repo.TryCreateReservationWithinCapacityAsync(DateTime.UtcNow.AddMinutes(15), "g", "ListingId=3;", 1));
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

                var reservations = new SponsoredListingReservationRepository(this.Db);
                var entries = new DirectoryEntryRepository(this.Db, new DirectoryEntriesAuditRepository(this.Db));
                this.Invoices = invoices;
                this.LatePayments = new SponsorLatePaymentService(
                    invoices, this.SponsoredListings, reservations, entries, this.BtcPay, this.Activator,
                    NullLogger<SponsorLatePaymentService>.Instance);

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
                    NullLogger<SponsoredListingCheckoutController>.Instance,
                    latePayments: this.LatePayments);
                this.NewRequest();
            }

            public ApplicationDbContext Db { get; }

            public ISponsoredListingRepository SponsoredListings { get; }

            public SponsoredListingActivator Activator { get; }

            public FakeBtcPay BtcPay { get; } = new ();

            public SponsorLatePaymentService LatePayments { get; }

            public ISponsoredListingInvoiceRepository Invoices { get; }

            public IServiceProvider SweepServices() => new ServiceCollection()
                .AddSingleton(this.Invoices)
                .AddSingleton<ISponsorLatePaymentService>(this.LatePayments)
                .BuildServiceProvider();

            public SponsoredListingCheckoutController Controller { get; }

            public async Task<IActionResult> ConfirmCheckoutAsync(string? email = null, Guid? rsvId = null, bool solveCaptcha = true, bool verifiedEarlier = false, bool keepSession = false, int offerId = OfferId, int entryId = EntryId)
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

                return await this.Controller.ConfirmedCheckoutAsync(entryId, offerId, "BTCPayServerNoJs", rsvId, email);
            }

            public async Task AddMainOfferAsync()
            {
                this.Db.SponsoredListingOffers.Add(new SponsoredListingOffer
                {
                    SponsoredListingOfferId = MainOfferId,
                    Description = "30 days main sponsor",
                    Days = 30,
                    Price = 900m,
                    PriceCurrency = Currency.USD,
                    IsEnabled = true,
                    SponsorshipType = SponsorshipType.MainSponsor,
                });
                await this.Db.SaveChangesAsync();
            }

            public async Task AddEligibleEntryAsync(int entryId)
            {
                this.Db.DirectoryEntries.Add(new DirectoryEntry
                {
                    DirectoryEntryId = entryId,
                    Name = $"Other {entryId}",
                    DirectoryEntryKey = $"other-{entryId}",
                    Link = $"https://other{entryId}.example",
                    SubCategoryId = SubCategoryId,
                    DirectoryStatus = DirectoryStatus.Verified,
                    CreateDate = DateTime.UtcNow.AddDays(-365),
                });
                await this.Db.SaveChangesAsync();
            }

            // Details in the same format the checkout writes ("Type=...; ListingId={id}; ..."), which
            // is how a hold is tied to its listing.
            public async Task<Guid> ReserveAsync(int entryId = EntryId, int minutes = 15, string? group = null)
            {
                group ??= ReservationGroupHelper.BuildReservationGroupName(SponsorshipType.SubcategorySponsor, SubCategoryId);
                var reservation = await new SponsoredListingReservationRepository(this.Db)
                    .CreateReservationAsync(DateTime.UtcNow.AddMinutes(minutes), group, $"Type=Test; ListingId={entryId}; ListingName=\"test\"");
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
            private string status = "New";
            private string additionalStatus = "None";
            private DateTimeOffset expiration = DateTimeOffset.UtcNow.AddHours(-3);
            private List<BtcPayPaymentEntry> payments = new ();

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
                Task.FromResult(new BtcPayInvoiceResponse
                {
                    Id = invoiceId,
                    Status = this.status,
                    AdditionalStatus = this.additionalStatus,
                    ExpirationTime = this.expiration.ToUnixTimeSeconds(),
                });

            // BTCPay's record of an invoice paid in full after its timer ran out.
            public void SetPaidLate(int minutesAfterExpiry, bool settled, string paid = "1.5")
            {
                this.SetExpired("PaidLate");
                this.payments = new List<BtcPayPaymentEntry>
                {
                    new ()
                    {
                        Value = paid,
                        Status = settled ? "Settled" : "Processing",
                        ReceivedDate = this.expiration.AddMinutes(minutesAfterExpiry).ToUnixTimeSeconds(),
                    },
                };
            }

            public void SetExpired(string additional)
            {
                this.status = "Expired";
                this.additionalStatus = additional;
            }

            public void SetSettled()
            {
                this.status = "Settled";
                this.additionalStatus = "None";
            }

            public Task<BtcPayInvoiceResponse> CreateInvoiceOnStoreAsync(string storeId, BtcPayInvoiceRequest request) => throw new NotSupportedException();

            public Task<BtcPayInvoiceResponse> GetInvoiceOnStoreAsync(string storeId, string invoiceId) => throw new NotSupportedException();

            public Task<BtcPayPaymentMethod?> GetXmrPaymentMethodOnStoreAsync(string storeId, string invoiceId) => throw new NotSupportedException();

            public Task<BtcPayPaymentMethod?> GetXmrPaymentMethodAsync(string invoiceId) =>
                Task.FromResult<BtcPayPaymentMethod?>(new BtcPayPaymentMethod { Rate = "200", PaymentMethodPaid = "1.5", TotalPaid = "1.5", Amount = "1.5", Payments = this.payments });

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
