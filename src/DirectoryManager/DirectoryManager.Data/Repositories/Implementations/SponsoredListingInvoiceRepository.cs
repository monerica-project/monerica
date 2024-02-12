using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingInvoiceRepository : ISponsoredListingInvoiceRepository
    {
        private readonly IApplicationDbContext context;

        public SponsoredListingInvoiceRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SponsoredListingInvoice?> GetByIdAsync(int id)
        {
            return await this.context.SponsoredListingInvoices.FindAsync(id);
        }

        public async Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId)
        {
            return await this.context.SponsoredListingInvoices
                                     .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
        }

        public async Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync()
        {
            return await this.context.SponsoredListingInvoices.ToListAsync();
        }

        public async Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice)
        {
            await this.context.SponsoredListingInvoices.AddAsync(invoice);
            await this.context.SaveChangesAsync();

            return invoice;
        }

        public async Task<bool> UpdateAsync(SponsoredListingInvoice invoice)
        {
            try
            {
                this.context.SponsoredListingInvoices.Update(invoice);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageAsync(int page, int pageSize)
        {
            var totalItems = await this.context.SponsoredListingInvoices.CountAsync();
            var invoices = await this.context.SponsoredListingInvoices
                                             .OrderByDescending(i => i.CreateDate)
                                             .Skip((page - 1) * pageSize)
                                             .Take(pageSize)
                                             .ToListAsync();

            return (invoices, totalItems);
        }

        public async Task DeleteAsync(int id)
        {
            var invoice = await this.GetByIdAsync(id);
            if (invoice != null)
            {
                this.context.SponsoredListingInvoices.Remove(invoice);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId)
        {
            var result = await this.context.SponsoredListingInvoices
                                     .FirstOrDefaultAsync(x => x.ProcessorInvoiceId == processorInvoiceId);

            return result ??
                throw new InvalidOperationException($"No SponsoredListingInvoice found for the provided {nameof(processorInvoiceId)}.");
        }
    }
}