using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingRepository
    {
        Task<SponsoredListing?> GetByIdAsync(int sponsoredListingId);
        Task<SponsoredListing?> GetByInvoiceIdAsync(int sponsoredListingInvoiceId);
        Task<IEnumerable<SponsoredListing>> GetAllAsync();
        Task<IEnumerable<SponsoredListing>> GetActiveSponsorsByTypeAsync(SponsorshipType sponsorshipType);
        Task<IEnumerable<SponsoredListing>> GetAllActiveSponsorsAsync();
        Task<DateTime?> GetNextExpirationDateAsync();
        Task<IEnumerable<SponsoredListing>> GetExpiringSponsorsWithinTimeAsync(TimeSpan timeSpan);
        Task<int> GetTotalCountAsync();
        Task<List<SponsoredListing>> GetPaginatedListingsAsync(int page, int pageSize);
        Task<SponsoredListing?> GetActiveSponsorAsync(int directoryEntryId, SponsorshipType sponsorshipType);
        Task<List<SponsoredListing>> GetSponsoredListingsForSubCategory(int subCategoryId);
        Task<SponsoredListing> CreateAsync(SponsoredListing sponsoredListing);
        Task<bool> UpdateAsync(SponsoredListing sponsoredListing);
        Task DeleteAsync(int sponsoredListingId);
        Task<bool> IsSponsoredListingActive(int directoryEntryId, SponsorshipType sponsorshipType);
        Task<Dictionary<int, DateTime>> GetLastChangeDatesBySubcategoryAsync();
        Task<Dictionary<int, DateTime>> GetLastChangeDatesByCategoryAsync();
        Task<DateTime?> GetLastChangeDateForMainSponsorAsync();
        Task<List<SponsoredListing>> GetSponsoredListingsForCategoryAsync(int categoryId);

        /// <summary>
        /// Count active sponsors of the given type.
        /// For MainSponsor: ignore typeId.
        /// For SubcategorySponsor: typeId is the SubCategoryId.
        /// For CategorySponsor:   typeId is the CategoryId.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<int> GetActiveSponsorsCountAsync(SponsorshipType sponsorshipType, int? typeId);

        Task<IEnumerable<SponsoredListing>> GetActiveSubCategorySponsorsAsync(int categoryId);

        Task<DateTime?> GetLastSponsorExpirationDateAsync();
      
        /// <summary>
        /// Gets the number of active sponsors **grouped by category**.
        /// </summary>
        Task<Dictionary<int, int>> GetActiveSponsorCountByCategoryAsync(SponsorshipType type);

        /// <summary>
        /// Gets the number of active sponsors **grouped by subcategory**.
        /// </summary>
        Task<Dictionary<int, int>> GetActiveSponsorCountBySubcategoryAsync(SponsorshipType type);
    }
}