using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingInvoiceRepository
    {
        Task<SponsoredListingInvoice?> GetByIdAsync(int sponsoredListingInvoiceId);
        Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId);
        Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId);
        Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync();
        Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageAsync(int page, int pageSize);
        Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice);
        Task<bool> UpdateAsync(SponsoredListingInvoice invoice);
        Task<InvoiceTotalsResult> GetTotalsPaidAsync(DateTime startDate, DateTime endDate);
        Task DeleteAsync(int sponsoredListingInvoiceId);
    }
}