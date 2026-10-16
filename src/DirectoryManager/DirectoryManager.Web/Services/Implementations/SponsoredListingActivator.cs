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

        public SponsoredListingActivator(
            ISponsoredListingInvoiceRepository invoiceRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            IMemoryCache cache,
            ILogger<SponsoredListingActivator> logger)
        {
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

            var active = await this.sponsoredListingRepository
                .GetActiveSponsorAsync(invoice.DirectoryEntryId, invoice.SponsorshipType).ConfigureAwait(false);

            if (active == null)
            {
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
