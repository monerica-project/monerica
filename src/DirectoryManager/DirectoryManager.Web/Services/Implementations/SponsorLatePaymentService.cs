using System.Net;
using BtcPayServer.API.Interfaces;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Accepts a sponsorship payment that reached BTCPay after the invoice timer ran out.
    /// Rules (see <see cref="LatePaymentRules"/>): paid in full, every payment confirmed, received
    /// no later than the grace period after expiry, and the slot is still free (or it is a renewal).
    /// Anything else is left for the owner and noted on the invoice.
    /// </summary>
    public class SponsorLatePaymentService : ISponsorLatePaymentService
    {
        // One acceptance at a time: the webhook and the sweeper can look at the same invoice together.
        private static readonly SemaphoreSlim Gate = new (1, 1);

        private readonly ISponsoredListingInvoiceRepository invoices;
        private readonly ISponsoredListingRepository sponsoredListings;
        private readonly ISponsoredListingReservationRepository reservations;
        private readonly IDirectoryEntryRepository entries;
        private readonly IBtcPayServerService btcPay;
        private readonly ISponsoredListingActivator activator;
        private readonly ILogger<SponsorLatePaymentService> logger;
        private readonly IEmailService? email;
        private readonly ICacheService? cacheService;
        private readonly TimeSpan grace;

        public SponsorLatePaymentService(
            ISponsoredListingInvoiceRepository invoices,
            ISponsoredListingRepository sponsoredListings,
            ISponsoredListingReservationRepository reservations,
            IDirectoryEntryRepository entries,
            IBtcPayServerService btcPay,
            ISponsoredListingActivator activator,
            ILogger<SponsorLatePaymentService> logger,
            IConfiguration? config = null,
            IEmailService? email = null,
            ICacheService? cacheService = null)
        {
            this.invoices = invoices;
            this.sponsoredListings = sponsoredListings;
            this.reservations = reservations;
            this.entries = entries;
            this.btcPay = btcPay;
            this.activator = activator;
            this.logger = logger;
            this.email = email;
            this.cacheService = cacheService;

            var minutes = config?.GetValue<int?>("Sponsorship:LatePaymentGraceMinutes");
            this.grace = minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : LatePaymentRules.DefaultGrace;
        }

        public async Task<LatePaymentOutcome> TryAcceptAsync(int sponsoredListingInvoiceId, CancellationToken ct = default)
        {
            await Gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await this.TryAcceptLockedAsync(sponsoredListingInvoiceId).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }

        private static bool IsOpenForLatePayment(SponsoredListingInvoice invoice) =>
            invoice.PaymentProcessor == PaymentProcessor.BTCPayServer
            && !invoice.IsTest
            && !string.IsNullOrWhiteSpace(invoice.ProcessorInvoiceId)
            && invoice.PaymentStatus is PaymentStatus.InvoiceCreated or PaymentStatus.Pending
                or PaymentStatus.UnderPayment or PaymentStatus.Expired or PaymentStatus.Canceled;

        private async Task<LatePaymentOutcome> TryAcceptLockedAsync(int sponsoredListingInvoiceId)
        {
            var invoice = await this.invoices.GetByIdAsync(sponsoredListingInvoiceId).ConfigureAwait(false);
            if (invoice == null || !IsOpenForLatePayment(invoice))
            {
                return LatePaymentOutcome.NotApplicable;
            }

            var processorInvoice = await this.btcPay.GetInvoiceAsync(invoice.ProcessorInvoiceId).ConfigureAwait(false);

            // Payment details are only needed (and only fetched) for an expired invoice BTCPay says was paid.
            var method = processorInvoice is { Status: "Expired", AdditionalStatus: "PaidLate" } || processorInvoice?.IsSettled == true
                ? await this.btcPay.GetXmrPaymentMethodAsync(invoice.ProcessorInvoiceId).ConfigureAwait(false)
                : null;
            var check = LatePaymentRules.Evaluate(processorInvoice, method, this.grace);

            switch (check.State)
            {
                case LatePaymentState.NotApplicable:
                    return LatePaymentOutcome.NotApplicable;
                case LatePaymentState.WaitingForConfirmations:
                    return LatePaymentOutcome.WaitingForConfirmations;
                case LatePaymentState.TooLate:
                    await this.NoteForOwnerAsync(invoice, "LatePaymentTooLate", check).ConfigureAwait(false);
                    return LatePaymentOutcome.TooLate;
            }

            // A settled BTCPay invoice is always honored (that is the normal webhook path, just
            // missed). Only a genuinely late payment has to find its slot still free.
            bool late = check.State == LatePaymentState.ReadyToAccept;
            bool renewal = await this.IsRenewalAsync(invoice).ConfigureAwait(false);
            if (late && !renewal)
            {
                var slot = await this.CheckSlotAsync(invoice).ConfigureAwait(false);
                if (slot == LatePaymentOutcome.SlotTaken)
                {
                    await this.NoteForOwnerAsync(invoice, "LatePaymentSlotTaken", check).ConfigureAwait(false);
                }

                if (slot != LatePaymentOutcome.Accepted)
                {
                    return slot;
                }

                // A new campaign that starts late still gets every day that was paid for.
                var now = DateTime.UtcNow;
                if (invoice.CampaignStartDate < now)
                {
                    var length = invoice.CampaignEndDate - invoice.CampaignStartDate;
                    invoice.CampaignStartDate = now;
                    invoice.CampaignEndDate = now + length;
                }
            }

            invoice.PaymentStatus = PaymentStatus.Paid;
            invoice.PaidInCurrency = Currency.XMR;
            if (check.SettledXmr > 0m)
            {
                invoice.PaidAmount = check.SettledXmr;
                invoice.OutcomeAmount = check.SettledXmr;
            }

            if (method != null && decimal.TryParse(method.Rate, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate > 0m)
            {
                invoice.PaymentRate = rate;
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(new
            {
                type = late ? "LatePaymentAccepted" : "InvoiceSettled",
                invoiceId = invoice.ProcessorInvoiceId,
                afterExpiration = late,
                source = "SponsorLatePaymentService",
                receivedUtc = check.LastReceivedUtc,
                minutesAfterExpiry = Math.Round(check.MinutesLate, 1),
                acceptedUtc = DateTime.UtcNow,
            });

            await this.activator.SaveAndActivateAsync(invoice).ConfigureAwait(false);

            this.logger.LogWarning(
                "Sponsorship invoice {InvoiceId} (BTCPay {ProcessorId}) accepted automatically: {Reason}. Listing {EntryId}, live until {End:u}.",
                invoice.InvoiceId,
                invoice.ProcessorInvoiceId,
                late ? $"paid {check.MinutesLate:F0} min after the timer ran out" : "BTCPay shows it settled",
                invoice.DirectoryEntryId,
                invoice.CampaignEndDate);

            if (late)
            {
                await this.TellBuyerAsync(invoice, check).ConfigureAwait(false);
            }

            return LatePaymentOutcome.Accepted;
        }

        private async Task<bool> IsRenewalAsync(SponsoredListingInvoice invoice) =>
            await this.sponsoredListings.GetActiveSponsorInScopeAsync(
                invoice.DirectoryEntryId, invoice.SponsorshipType, ScopeId(invoice)).ConfigureAwait(false) != null;

        private static int? ScopeId(SponsoredListingInvoice invoice) => invoice.SponsorshipType switch
        {
            SponsorshipType.CategorySponsor => invoice.CategoryId,
            SponsorshipType.SubcategorySponsor => invoice.SubCategoryId,
            _ => null,
        };

        // Accepted = a slot is free. A sold slot is final (owner decides); a slot another buyer is
        // checking out right now is temporary and is simply looked at again on the next pass.
        private async Task<LatePaymentOutcome> CheckSlotAsync(SponsoredListingInvoice invoice)
        {
            var scopeId = ScopeId(invoice);
            var max = SponsoredListingCheckoutHelper.GetMaxSlotsForType(invoice.SponsorshipType);
            var taken = await this.sponsoredListings.GetActiveSponsorsCountAsync(invoice.SponsorshipType, scopeId).ConfigureAwait(false);
            if (taken >= max)
            {
                return LatePaymentOutcome.SlotTaken;
            }

            var group = ReservationGroupHelper.BuildReservationGroupName(invoice.SponsorshipType, scopeId);
            var heldByOthers = await this.reservations
                .GetActiveReservationsCountExcludingEntryAsync(group, invoice.DirectoryEntryId).ConfigureAwait(false);
            if (taken + heldByOthers >= max)
            {
                return LatePaymentOutcome.SlotHeld;
            }

            if (invoice.SponsorshipType == SponsorshipType.MainSponsor)
            {
                var entry = await this.entries.GetByIdAsync(invoice.DirectoryEntryId).ConfigureAwait(false);
                var subCategoryId = invoice.SubCategoryId is > 0 ? invoice.SubCategoryId.Value : entry?.SubCategoryId ?? 0;
                var mains = await this.sponsoredListings.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor).ConfigureAwait(false);
                var inSubcategory = mains.Count(s =>
                    (s.SubCategoryId is > 0 ? s.SubCategoryId.Value : s.DirectoryEntry?.SubCategoryId ?? 0) == subCategoryId);
                if (inSubcategory >= Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
                {
                    return LatePaymentOutcome.SlotTaken;
                }

                var subGroup = ReservationGroupHelper.BuildMainSponsorSubcategoryGroupName(subCategoryId);
                if (await this.reservations.GetActiveReservationsCountExcludingEntryAsync(subGroup, invoice.DirectoryEntryId).ConfigureAwait(false) > 0)
                {
                    return LatePaymentOutcome.SlotHeld;
                }
            }

            return LatePaymentOutcome.Accepted;
        }

        // Written once on the invoice (visible on the admin invoice page) and logged loudly.
        private async Task NoteForOwnerAsync(SponsoredListingInvoice invoice, string type, LatePaymentCheck check)
        {
            if ((invoice.PaymentResponse ?? string.Empty).Contains(type, StringComparison.Ordinal))
            {
                return;
            }

            this.logger.LogCritical(
                "LATE SPONSORSHIP PAYMENT NEEDS THE OWNER ({Type}): invoice {InvoiceId} (BTCPay {ProcessorId}, listing {EntryId}) was paid {Minutes:F0} min after its timer ran out and was NOT activated automatically.",
                type, invoice.InvoiceId, invoice.ProcessorInvoiceId, invoice.DirectoryEntryId, check.MinutesLate);

            invoice.PaymentResponse = JsonConvert.SerializeObject(new
            {
                type,
                invoiceId = invoice.ProcessorInvoiceId,
                afterExpiration = true,
                source = "SponsorLatePaymentService",
                receivedUtc = check.LastReceivedUtc,
                minutesAfterExpiry = Math.Round(check.MinutesLate, 1),
                notedUtc = DateTime.UtcNow,
            });
            await this.invoices.UpdateAsync(invoice).ConfigureAwait(false);
        }

        // Best effort: the listing is already live, a failed email must not undo that.
        private async Task TellBuyerAsync(SponsoredListingInvoice invoice, LatePaymentCheck check)
        {
            if (this.email == null || string.IsNullOrWhiteSpace(invoice.Email))
            {
                return;
            }

            try
            {
                var siteName = this.cacheService == null
                    ? string.Empty
                    : await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName).ConfigureAwait(false);
                siteName = string.IsNullOrWhiteSpace(siteName) ? "our directory" : siteName.Trim();

                var listing = invoice.DirectoryEntry?.Name
                    ?? (await this.entries.GetByIdAsync(invoice.DirectoryEntryId).ConfigureAwait(false))?.Name
                    ?? "your listing";
                var until = invoice.CampaignEndDate.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
                var subject = $"Payment received: {listing} sponsorship on {siteName} is live";

                var text =
                    $"Your payment for \"{invoice.InvoiceDescription}\" arrived after the invoice timer had run out.\n\n" +
                    $"We accepted it anyway. You do not need to pay again or contact us.\n\n" +
                    $"Listing: {listing}\n" +
                    $"Sponsored until: {until}\n" +
                    $"Order ID: {invoice.InvoiceId}\n\n" +
                    $"Thank you for supporting {siteName}.";

                string E(string s) => WebUtility.HtmlEncode(s);
                var html =
                    $"<p>Your payment for <strong>{E(invoice.InvoiceDescription)}</strong> arrived after the invoice timer had run out.</p>" +
                    $"<p><strong>We accepted it anyway.</strong> You do not need to pay again or contact us.</p>" +
                    $"<p>Listing: <strong>{E(listing)}</strong><br>Sponsored until: <strong>{E(until)}</strong><br>Order ID: {E(invoice.InvoiceId.ToString())}</p>" +
                    $"<p>Thank you for supporting {E(siteName)}.</p>";

                await this.email.SendEmailAsync(subject, text, html, new List<string> { invoice.Email.Trim() }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Late payment accepted for invoice {InvoiceId}, but the confirmation email failed.", invoice.InvoiceId);
            }
        }
    }
}
