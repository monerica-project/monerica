using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    public class SponsoredListingActivator : ISponsoredListingActivator
    {
        private readonly ISponsoredListingInvoiceRepository invoiceRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IMemoryCache cache;
        private readonly ILogger<SponsoredListingActivator> logger;

        private readonly IStaticPublishService? staticPublish;

        public SponsoredListingActivator(
            ISponsoredListingInvoiceRepository invoiceRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            IMemoryCache cache,
            ILogger<SponsoredListingActivator> logger,
            IStaticPublishService? staticPublish = null)
        {
            this.staticPublish = staticPublish;
            this.invoiceRepository = invoiceRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.cache = cache;
            this.logger = logger;
        }

        public bool NeedsActivation(SponsoredListingInvoice invoice) =>
            SponsoredListingCheckoutHelper.IsPaidOrOverpaid(invoice.PaymentStatus)
            && (invoice.SponsoredListingId == null || invoice.SponsoredListingId == 0);

        public async Task SaveAndActivateAsync(SponsoredListingInvoice invoice)
        {
            await this.SaveInvoiceAsync(invoice).ConfigureAwait(false);

            if (!SponsoredListingCheckoutHelper.IsPaidOrOverpaid(invoice.PaymentStatus))
            {
                return;
            }

            // This invoice has already been turned into (or added to) a sponsored listing. Its paid
            // period is counted exactly once, however often the payment event or an admin edit repeats.
            if (invoice.SponsoredListingId is > 0)
            {
                return;
            }

            // Already activated by an earlier delivery of the same payment event.
            var linked = await this.sponsoredListingRepository
                .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId).ConfigureAwait(false);
            if (linked != null)
            {
                if (invoice.SponsoredListingId != linked.SponsoredListingId)
                {
                    invoice.SponsoredListingId = linked.SponsoredListingId;
                    await this.SaveInvoiceAsync(invoice).ConfigureAwait(false);
                }

                return;
            }

            // A campaign that has already ended has nothing to activate. This also keeps an admin
            // edit of an old invoice (from before invoices were linked to listings) from adding a
            // dead listing row.
            if (invoice.CampaignEndDate <= DateTime.UtcNow)
            {
                this.logger.LogWarning(
                    "Invoice {InvoiceId} is paid but its campaign ended {End:u}; no listing created.",
                    invoice.InvoiceId, invoice.CampaignEndDate);
                return;
            }

            // Only a campaign in the SAME slot (category / subcategory) as this invoice is a renewal.
            // A listing that moved (sub)category keeps its old campaign untouched and gets a new one.
            int? scopeId = invoice.SponsorshipType switch
            {
                SponsorshipType.CategorySponsor => invoice.CategoryId,
                SponsorshipType.SubcategorySponsor => invoice.SubCategoryId,
                _ => null,
            };
            var active = await this.sponsoredListingRepository
                .GetActiveSponsorInScopeAsync(invoice.DirectoryEntryId, invoice.SponsorshipType, scopeId).ConfigureAwait(false);

            if (active == null)
            {
                // A paid order is always honored. If the slot filled up while this payment was on its
                // way, say so loudly: the owner has to sort out two sponsors in one slot.
                var max = SponsoredListingCheckoutHelper.GetMaxSlotsForType(invoice.SponsorshipType);
                var taken = await this.sponsoredListingRepository
                    .GetActiveSponsorsCountAsync(invoice.SponsorshipType, scopeId).ConfigureAwait(false);
                if (taken >= max)
                {
                    this.logger.LogCritical(
                        "OVERSOLD SLOT: invoice {InvoiceId} (listing {EntryId}, {Type}, scope {Scope}) was paid but {Taken} of {Max} slots were already taken. Activated anyway; needs the owner's attention.",
                        invoice.InvoiceId, invoice.DirectoryEntryId, invoice.SponsorshipType, scopeId, taken, max);
                }

                var created = await this.sponsoredListingRepository.CreateAsync(new SponsoredListing
                {
                    DirectoryEntryId = invoice.DirectoryEntryId,
                    CampaignStartDate = invoice.CampaignStartDate,
                    CampaignEndDate = invoice.CampaignEndDate,
                    SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                    SponsorshipType = invoice.SponsorshipType,
                    SubCategoryId = invoice.SubCategoryId,
                    CategoryId = invoice.CategoryId,
                }).ConfigureAwait(false);

                invoice.SponsoredListingId = created.SponsoredListingId;
                this.logger.LogInformation(
                    "Invoice {InvoiceId} activated: new sponsored listing {ListingId}.",
                    invoice.InvoiceId, created.SponsoredListingId);
            }
            else
            {
                // A renewal: the buyer already sponsors this slot, so the paid period extends it.
                // Two invoices bought for the same period (two tabs, or a second one while the first
                // waited for confirmations) both carry the same dates. The later one is moved to
                // start where the sponsorship now ends, so every paid day is delivered.
                var paidLength = invoice.CampaignEndDate - invoice.CampaignStartDate;
                if (paidLength > TimeSpan.Zero && invoice.CampaignStartDate < active.CampaignEndDate)
                {
                    invoice.CampaignStartDate = active.CampaignEndDate;
                    invoice.CampaignEndDate = active.CampaignEndDate + paidLength;
                }

                if (invoice.CampaignEndDate > active.CampaignEndDate)
                {
                    active.CampaignEndDate = invoice.CampaignEndDate;
                }

                active.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId;

                if (!await this.sponsoredListingRepository.UpdateAsync(active).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        $"Could not extend sponsored listing {active.SponsoredListingId} for invoice {invoice.InvoiceId}.");
                }

                invoice.SponsoredListingId = active.SponsoredListingId;
                this.logger.LogInformation(
                    "Invoice {InvoiceId} activated: extended sponsored listing {ListingId} to {End:u}.",
                    invoice.InvoiceId, active.SponsoredListingId, active.CampaignEndDate);
            }

            await this.SaveInvoiceAsync(invoice).ConfigureAwait(false);
            SponsorCacheHelper.ClearSponsorCaches(this.cache);

            // The sponsor ticker and boxes are on every page: refresh the whole mirror once.
            if (this.staticPublish != null)
            {
                await this.staticPublish.RequestFullCrawlAsync($"sponsorship paid (invoice {invoice.InvoiceId})").ConfigureAwait(false);
            }
        }

        private async Task SaveInvoiceAsync(SponsoredListingInvoice invoice)
        {
            if (!await this.invoiceRepository.UpdateAsync(invoice).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Could not save sponsorship invoice {invoice.InvoiceId}.");
            }
        }
    }
}
