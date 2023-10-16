using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingInvoiceRepository
    {
        Task<SponsoredListingInvoice?> GetByIdAsync(int id);
        Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId);
        Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId);
        Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync();
        Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice);
        Task<bool> UpdateAsync(SponsoredListingInvoice invoice);
        Task DeleteAsync(int id);
    }
}