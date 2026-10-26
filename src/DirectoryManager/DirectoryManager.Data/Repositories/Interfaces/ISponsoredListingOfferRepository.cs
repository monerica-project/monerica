using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOfferRepository
    {
        Task<IEnumerable<SponsoredListingOffer>> GetAllAsync();
        Task<IEnumerable<SponsoredListingOffer>> GetAllByTypeAsync(SponsorshipType sponsorshipType);

        /// <summary>
        /// The price list for one placement: the main sponsor slot, one category page or one
        /// subcategory page. A category or subcategory with its own enabled prices uses them;
        /// anything else uses the standard (default) price list for that placement type.
        /// </summary>
        /// <param name="sponsorshipType">The placement type.</param>
        /// <param name="placementId">CategoryId for a category sponsor, SubCategoryId for a
        /// subcategory sponsor, null for the main sponsor.</param>
        Task<IEnumerable<SponsoredListingOffer>> GetOffersForPlacementAsync(SponsorshipType sponsorshipType, int? placementId);
        Task<SponsoredListingOffer> GetByIdAsync(int sponsoredListingOfferId);
        Task CreateAsync(SponsoredListingOffer offer);
        Task UpdateAsync(SponsoredListingOffer offer);
        Task DeleteOfferAsync(int sponsoredListingOfferId);
        Task<DateTime?> GetLastModifiedDateAsync();
    }
}