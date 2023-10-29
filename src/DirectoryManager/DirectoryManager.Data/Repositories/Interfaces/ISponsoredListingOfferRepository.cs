using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOfferRepository
    {
        Task<IEnumerable<SponsoredListingOffer>> GetAllOffersAsync();
        Task<SponsoredListingOffer> GetOfferByIdAsync(int id);
        Task AddOfferAsync(SponsoredListingOffer offer);
        Task UpdateOfferAsync(SponsoredListingOffer offer);
        Task DeleteOfferAsync(int id);
    }
}