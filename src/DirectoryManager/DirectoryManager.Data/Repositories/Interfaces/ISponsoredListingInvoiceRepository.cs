using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingInvoiceRepository
    {
        Task<SponsoredListingInvoice?> GetByIdAsync(int id);
        Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync();
        Task CreateAsync(SponsoredListingInvoice invoice);
        Task UpdateAsync(SponsoredListingInvoice invoice);
        Task DeleteAsync(int id);
    }
}