using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingRepository
    {
        Task<SponsoredListing?> GetByIdAsync(int id);
        Task<SponsoredListing?> GetByInvoiceIdAsync(int sponsoredListingInvoiceId);
        Task<IEnumerable<SponsoredListing>> GetAllAsync();
        Task<IEnumerable<SponsoredListing>> GetAllActiveListingsAsync();
        Task<int> GetTotalCountAsync();
        Task<List<SponsoredListing>> GetPaginatedListingsAsync(int page, int pageSize);

        Task<SponsoredListing> CreateAsync(SponsoredListing sponsoredListing);
        Task<bool> UpdateAsync(SponsoredListing sponsoredListing);
        Task DeleteAsync(int id);
    }
}