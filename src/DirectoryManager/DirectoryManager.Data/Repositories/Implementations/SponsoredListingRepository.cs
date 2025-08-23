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

        public async Task<IEnumerable<SponsoredListing>> GetActiveSponsorsByTypeAsync(SponsorshipType sponsorshipType)
        {
            var currentDate = DateTime.UtcNow;

            return await this.context.SponsoredListings
                .AsNoTracking()
                                     .Include(x => x.DirectoryEntry) // Include DirectoryEntry navigation property
                                     .ThenInclude(x => x.SubCategory!)
                                     .ThenInclude(x => x.Category!)
                                     .Where(x => x.SponsorshipType == sponsorshipType &&
                                                 x.CampaignStartDate <= currentDate &&
                                                 x.CampaignEndDate >= currentDate) // Filter active listings
                                     .OrderByDescending(x => x.CampaignEndDate) // Sort primarily by end date
                                     .ThenByDescending(x => x.CampaignStartDate) // Then by start date
                                     .ToListAsync();
        }

        public async Task<IEnumerable<SponsoredListing>> GetAllActiveSponsorsAsync()
        {
            var currentDate = DateTime.UtcNow;

            return await this.context.SponsoredListings
                                     .Include(x => x.DirectoryEntry!)
                                     .ThenInclude(x => x.SubCategory!)
                                     .ThenInclude(x => x.Category!)
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

        public async Task<int> GetActiveSponsorsCountAsync(SponsorshipType sponsorshipType, int? typeId)
        {
            var now = DateTime.UtcNow;
            IQueryable<SponsoredListing> query = this.context.SponsoredListings
                .Where(x =>
                    x.SponsorshipType == sponsorshipType &&
                    x.CampaignStartDate <= now &&
                    x.CampaignEndDate >= now);

            switch (sponsorshipType)
            {
                case SponsorshipType.MainSponsor:
                    // nothing extra—typeId is ignored
                    break;

                case SponsorshipType.SubcategorySponsor:
                    if (!typeId.HasValue)
                    {
                        throw new ArgumentException(
                            "SubcategorySponsor requires a subcategory ID.", nameof(typeId));
                    }

                    query = query.Where(x => x.SubCategoryId == typeId.Value);
                    break;

                case SponsorshipType.CategorySponsor:
                    if (!typeId.HasValue)
                    {
                        throw new ArgumentException(
                            "CategorySponsor requires a category ID.", nameof(typeId));
                    }

                    query = query.Where(x =>
                        x.DirectoryEntry!.SubCategory!.CategoryId == typeId.Value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sponsorshipType), sponsorshipType, "Unknown sponsorship type");
            }

            return await query.CountAsync();
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

        public Task<SponsoredListing?> GetActiveSponsorAsync(int directoryEntryId, SponsorshipType sponsorshipType)
        {
            var now = DateTime.UtcNow;

            return this.context.SponsoredListings
                .FirstOrDefaultAsync(x => x.DirectoryEntryId == directoryEntryId &&
                                          x.SponsorshipType == sponsorshipType &&
                                          x.CampaignStartDate <= now &&
                                          x.CampaignEndDate >= now);
        }

        public async Task<DateTime?> GetNextExpirationDateAsync()
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

        public async Task<Dictionary<int, DateTime>> GetLastChangeDatesByCategoryAsync()
        {
            var lastModifiedDates = await this.context.SponsoredListings
                .Where(x => x.CategoryId.HasValue && x.SponsorshipType == SponsorshipType.CategorySponsor) // Ensure SubCategoryId is not null
                .GroupBy(x => x.CategoryId) // Group by nullable SubCategoryId
                .Select(g => new
                {
                    CategoryId = g.Key ?? 0, // Use the null-coalescing operator as a fallback
                    LastModified = g.Max(x => x.UpdateDate.HasValue && x.UpdateDate > x.CreateDate
                                                ? x.UpdateDate.Value
                                                : x.CreateDate)
                })
                .Where(x => x.CategoryId != 0) // Filter out any entries with the fallback value
                .ToListAsync();

            // If there are no items or no items with subcategories, the dictionary will be empty
            return lastModifiedDates.ToDictionary(x => x.CategoryId, x => x.LastModified);
        }

        public async Task<Dictionary<int, DateTime>> GetLastChangeDatesBySubcategoryAsync()
        {
            var lastModifiedDates = await this.context.SponsoredListings
                .Where(x => x.SubCategoryId.HasValue && x.SponsorshipType == SponsorshipType.SubcategorySponsor) // Ensure SubCategoryId is not null
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

        public async Task<IEnumerable<SponsoredListing>> GetExpiringSponsorsWithinTimeAsync(TimeSpan timeSpan)
        {
            var currentDate = DateTime.UtcNow;
            var targetDate = currentDate + timeSpan;

            return await this.context.SponsoredListings
                .Include(x => x.DirectoryEntry) // Include related DirectoryEntry for additional data if needed
                .Where(x => x.CampaignEndDate > currentDate && x.CampaignEndDate <= targetDate)
                .OrderBy(x => x.CampaignEndDate) // Order by the soonest to expire
                .ToListAsync();
        }

        public async Task<List<SponsoredListing>> GetSponsoredListingsForCategoryAsync(int categoryId)
        {
            var now = DateTime.UtcNow;
            return await this.context.SponsoredListings
                .Include(x => x.DirectoryEntry)
                    .ThenInclude(e => e.SubCategory)
                        .ThenInclude(sc => sc.Category)
                .Where(x =>
                    x.SponsorshipType == SponsorshipType.CategorySponsor &&
                    x.DirectoryEntry.SubCategory.CategoryId == categoryId &&
                    x.CampaignStartDate <= now &&
                    x.CampaignEndDate >= now)
                .OrderByDescending(x => x.CampaignEndDate)
                .ThenByDescending(x => x.CampaignStartDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<SponsoredListing>> GetActiveSubCategorySponsorsAsync(int categoryId)
        {
            // pull all SubCategorySponsor entries, then filter by category
            var all = await this.GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);
            return all
              .Where(s => s.DirectoryEntry?.SubCategory?.CategoryId == categoryId);
        }

        public async Task<DateTime?> GetLastSponsorExpirationDateAsync()
        {
            var now = DateTime.UtcNow;

            return await this.context.SponsoredListings
                // only look at campaigns that have already ended
                .Where(x => x.CampaignEndDate < now)
                // pick the one with the most recent end date
                .OrderByDescending(x => x.CampaignEndDate)
                .Select(x => (DateTime?)x.CampaignEndDate)
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<int, int>> GetActiveSponsorCountByCategoryAsync(SponsorshipType type)
        {
            return await this.context.SponsoredListings
              .Where(s => s.SponsorshipType == type && s.CampaignEndDate > DateTime.UtcNow)
              .GroupBy(s => s.CategoryId)
              .ToDictionaryAsync(g => g.Key.Value, g => g.Count());
        }

        public async Task<Dictionary<int, int>> GetActiveSponsorCountBySubcategoryAsync(SponsorshipType type)
        {
            return await this.context.SponsoredListings
              .Where(s => s.SponsorshipType == type && s.CampaignEndDate > DateTime.UtcNow)
              .GroupBy(s => s.SubCategoryId)
              .ToDictionaryAsync(g => g.Key.Value, g => g.Count());
        }
    }
}