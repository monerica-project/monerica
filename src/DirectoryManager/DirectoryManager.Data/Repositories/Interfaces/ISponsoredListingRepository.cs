using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

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

        /// <summary>
        /// The earliest campaign start date across ALL of an entry's sponsorships (paid
        /// listings), i.e. when they first became a sponsor. Null if they never sponsored.
        /// Used to show "Sponsor since {date}" on the entry's profile.
        /// </summary>
        Task<DateTime?> GetSponsorSinceDateAsync(int directoryEntryId);

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

        Task<Dictionary<int, int>> GetActiveSponsorCountByCategoryAsync(SponsorshipType type);

        Task<Dictionary<int, int>> GetActiveSponsorCountBySubcategoryAsync(SponsorshipType type);

        Task<List<SponsorTickerItemVm>> GetSponsorTickerItemsAsync();

        /// <summary>Distinct directory-entry IDs that CURRENTLY hold an active sponsored
        /// listing (of any type) — used to let active sponsors extend or re-sponsor without
        /// meeting the standard eligibility requirements. Expired or refunded listings do not
        /// count, so a lapsed/refunded buyer is NOT grandfathered.</summary>
        Task<HashSet<int>> GetActivelySponsoredDirectoryEntryIdsAsync();

        /// <summary>True if the entry currently holds an active sponsored listing of any type.</summary>
        Task<bool> HasActiveSponsorshipAsync(int directoryEntryId);
    }
}