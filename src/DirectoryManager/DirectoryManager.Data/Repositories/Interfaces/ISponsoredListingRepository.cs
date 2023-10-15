using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingRepository
    {
        Task<SponsoredListing?> GetByIdAsync(int id);
        Task<IEnumerable<SponsoredListing>> GetAllAsync();
        Task CreateAsync(SponsoredListing sponsoredListing);
        Task UpdateAsync(SponsoredListing sponsoredListing);
        Task DeleteAsync(int id);
    }
}