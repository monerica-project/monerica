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
        Task<int> GetActiveSponsorsCountAsync(SponsorshipType sponsorshipType, int? subCategoryId);
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
        Task<Dictionary<int, DateTime>> GetLastChangeDatesBySubCategoryAsync();
        Task<DateTime?> GetLastChangeDateForMainSponsorAsync();
    }
}