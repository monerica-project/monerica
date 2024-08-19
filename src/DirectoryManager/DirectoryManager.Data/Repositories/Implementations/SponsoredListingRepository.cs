using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingRepository : ISponsoredListingRepository
    {
        private readonly IApplicationDbContext context;

        public SponsoredListingRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SponsoredListing?> GetByIdAsync(int sponsoredListingId)
        {
            return await this.context.SponsoredListings.FindAsync(sponsoredListingId);
        }

        public async Task<SponsoredListing?> GetByInvoiceIdAsync(int sponsoredListingInvoiceId)
        {
            return await this.context.SponsoredListings
                                     .FirstOrDefaultAsync(x => x.SponsoredListingInvoiceId == sponsoredListingInvoiceId);
        }

        public async Task<IEnumerable<SponsoredListing>> GetAllActiveListingsAsync(SponsorshipType sponsorshipType)
        {
            var currentDate = DateTime.UtcNow;

            return await this.context.SponsoredListings
                                     .Include(x => x.DirectoryEntry) // Include DirectoryEntry navigation property
                                     .Where(x => x.SponsorshipType == sponsorshipType &&
                                                 x.CampaignStartDate <= currentDate &&
                                                 x.CampaignEndDate >= currentDate) // Filter active listings
                                     .OrderByDescending(x => x.CampaignEndDate) // Sort primarily by end date
                                     .ThenByDescending(x => x.CampaignStartDate) // Then by start date
                                     .ToListAsync();
        }

        public async Task<IEnumerable<SponsoredListing>> GetAllActiveListingsAsync()
        {
            var currentDate = DateTime.UtcNow;

            return await this.context.SponsoredListings
                                     .Include(x => x.DirectoryEntry) // Include DirectoryEntry navigation property
                                     .Where(x => x.CampaignStartDate <= currentDate &&
                                                 x.CampaignEndDate >= currentDate) // Filter active listings
                                     .OrderByDescending(x => x.CampaignEndDate) // Sort primarily by end date
                                     .ThenByDescending(x => x.CampaignStartDate) // Then by start date
                                     .ToListAsync();
        }

        public async Task<List<SponsoredListing>> GetSponsoredListingsForSubCategory(int subCategoryId)
        {
            var currentDate = DateTime.UtcNow;

            var subCategorySponsors = await this.context.SponsoredListings
                                     .Include(x => x.DirectoryEntry) // Include DirectoryEntry navigation property
                                     .Where(x => x.SponsorshipType == SponsorshipType.SubcategorySponsor &&
                                                 x.SubCategoryId == subCategoryId &&
                                                 x.CampaignStartDate <= currentDate &&
                                                 x.CampaignEndDate >= currentDate) // Filter active listings
                                     .OrderByDescending(x => x.CampaignEndDate) // Sort primarily by end date
                                     .ThenByDescending(x => x.CampaignStartDate) // Then by start date
                                     .ToListAsync();

            return subCategorySponsors;
        }

        public async Task<int> GetActiveListingsCountAsync(SponsorshipType sponsorshipType, int? subCategoryId)
        {
            var currentDate = DateTime.UtcNow;
            var totalActive = 0;

            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                totalActive = await this.context.SponsoredListings
                         .Include(x => x.DirectoryEntry)
                         .Where(x => x.SponsorshipType == sponsorshipType &&
                                     x.CampaignStartDate <= currentDate &&
                                     x.CampaignEndDate >= currentDate)
                         .OrderByDescending(x => x.CampaignEndDate)
                         .ThenByDescending(x => x.CampaignStartDate)
                         .CountAsync();
            }
            else if (sponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                totalActive = await this.context.SponsoredListings
                         .Include(x => x.DirectoryEntry)
                         .Where(x => x.SponsorshipType == sponsorshipType &&
                                     x.SubCategoryId == subCategoryId &&
                                     x.CampaignStartDate <= currentDate &&
                                     x.CampaignEndDate >= currentDate)
                         .OrderByDescending(x => x.CampaignEndDate)
                         .ThenByDescending(x => x.CampaignStartDate)
                         .CountAsync();
            }
            else
            {
                throw new ArgumentException(sponsorshipType.ToString());
            }

            return totalActive;
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await this.context.SponsoredListings.CountAsync();
        }

        public async Task<List<SponsoredListing>> GetPaginatedListingsAsync(int page, int pageSize)
        {
            return await this.context.SponsoredListings
                .OrderByDescending(x => x.CampaignEndDate)
                .Include(l => l.DirectoryEntry)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<SponsoredListing>> GetAllAsync()
        {
            return await this.context.SponsoredListings.ToListAsync();
        }

        public async Task<SponsoredListing> CreateAsync(SponsoredListing sponsoredListing)
        {
            await this.context.SponsoredListings.AddAsync(sponsoredListing);
            await this.context.SaveChangesAsync();

            return sponsoredListing;
        }

        public async Task<bool> UpdateAsync(SponsoredListing sponsoredListing)
        {
            try
            {
                this.context.SponsoredListings.Update(sponsoredListing);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task DeleteAsync(int sponsoredListingId)
        {
            var sponsoredListing = await this.GetByIdAsync(sponsoredListingId);
            if (sponsoredListing != null)
            {
                this.context.SponsoredListings.Remove(sponsoredListing);
                await this.context.SaveChangesAsync();
            }
        }

        public Task<SponsoredListing?> GetActiveListing(int directoryEntryId, SponsorshipType sponsorshipType)
        {
            var now = DateTime.UtcNow;

            return this.context.SponsoredListings
                .FirstOrDefaultAsync(x => x.DirectoryEntryId == directoryEntryId &&
                                          x.SponsorshipType == sponsorshipType &&
                                          x.CampaignStartDate <= now &&
                                          x.CampaignEndDate >= now);
        }

        public async Task<DateTime?> GetNextExpirationDate()
        {
            var now = DateTime.UtcNow;

            var nextExpirationDate = await this.context.SponsoredListings
                .Where(x => x.CampaignEndDate > now) // Filter for future expiration dates
                .OrderBy(x => x.CampaignEndDate) // Order by the closest expiration date
                .Select(x => x.CampaignEndDate) // Select only the expiration date
                .FirstOrDefaultAsync(); // Get the closest one

            return nextExpirationDate; // This will be null if there are no future expirations
        }

        public async Task<bool> IsSponsoredListingActive(int directoryEntryId, SponsorshipType sponsorshipType)
        {
            var now = DateTime.UtcNow;

            var result = await this.context
                                   .SponsoredListings
                                   .Where(x => x.CampaignStartDate <= now &&
                                                x.CampaignEndDate >= now &&
                                                x.DirectoryEntryId == directoryEntryId &&
                                                x.SponsorshipType == sponsorshipType)
                                   .FirstOrDefaultAsync();

            return result != null;
        }

        public async Task<Dictionary<int, DateTime>> GetLastChangeDatesBySubCategoryAsync()
        {
            var lastModifiedDates = await this.context.SponsoredListings
                .Where(x => x.SubCategoryId.HasValue) // Ensure SubCategoryId is not null
                .GroupBy(x => x.SubCategoryId) // Group by nullable SubCategoryId
                .Select(g => new
                {
                    SubCategoryId = g.Key ?? 0, // Use the null-coalescing operator as a fallback
                    LastModified = g.Max(x => x.UpdateDate.HasValue && x.UpdateDate > x.CreateDate
                                                ? x.UpdateDate.Value
                                                : x.CreateDate)
                })
                .Where(x => x.SubCategoryId != 0) // Filter out any entries with the fallback value
                .ToListAsync();

            // If there are no items or no items with subcategories, the dictionary will be empty
            return lastModifiedDates.ToDictionary(x => x.SubCategoryId, x => x.LastModified);
        }

        public async Task<DateTime?> GetLastChangeDateForMainSponsorAsync()
        {
            var lastChangeDate = await this.context.SponsoredListings
                .Where(x => x.SponsorshipType == SponsorshipType.MainSponsor) // Filter for MainSponsor type
                .Select(x => (DateTime?)((x.UpdateDate.HasValue && x.UpdateDate > x.CreateDate)
                    ? x.UpdateDate.Value
                    : x.CreateDate))
                .OrderByDescending(date => date)
                .FirstOrDefaultAsync();

            return lastChangeDate;
        }
    }
}