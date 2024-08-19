using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOfferRepository
    {
        Task<IEnumerable<SponsoredListingOffer>> GetAllAsync();
        Task<IEnumerable<SponsoredListingOffer>> GetAllByTypeAsync(SponsorshipType sponsorshipType);
        Task<IEnumerable<SponsoredListingOffer>> GetByTypeAndSubCategoryAsync(SponsorshipType sponsorshipType, int? subcategoryId);
        Task<SponsoredListingOffer> GetByIdAsync(int sponsoredListingOfferId);
        Task CreateAsync(SponsoredListingOffer offer);
        Task UpdateAsync(SponsoredListingOffer offer);
        Task DeleteOfferAsync(int sponsoredListingOfferId);
    }
}