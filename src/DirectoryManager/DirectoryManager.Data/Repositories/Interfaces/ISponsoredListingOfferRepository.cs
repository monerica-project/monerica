using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOfferRepository
    {
        Task<IEnumerable<SponsoredListingOffer>> GetAllAsync();
        Task<SponsoredListingOffer> GetByIdAsync(int id);
        Task CreateAsync(SponsoredListingOffer offer);
        Task UpdateAsync(SponsoredListingOffer offer);
        Task DeleteOfferAsync(int id);
    }
}