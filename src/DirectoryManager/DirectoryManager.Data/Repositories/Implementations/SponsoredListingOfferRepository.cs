using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingOfferRepository : ISponsoredListingOfferRepository
    {
        private readonly IApplicationDbContext context;

        public SponsoredListingOfferRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<SponsoredListingOffer>> GetAllAsync()
        {
            var offers = await this.context.SponsoredListingOffers
                .Include(slo => slo.Category)
                .Include(slo => slo.Subcategory!)
                    .ThenInclude(sub => sub.Category)
                .ToListAsync();

            return offers
                .OrderBy(o => o.SponsorshipType.ToString())
                .ThenBy(o => SponsoredListingOfferPlacement.SortKey(o))
                .ThenBy(o => o.Days)
                .ToList();
        }

        public async Task<IEnumerable<SponsoredListingOffer>> GetAllByTypeAsync(SponsorshipType sponsorshipType)
        {
            var offers = await this.context.SponsoredListingOffers
                .Include(slo => slo.Category)
                .Include(slo => slo.Subcategory!)
                    .ThenInclude(sub => sub.Category)
                .Where(x => x.SponsorshipType == sponsorshipType && x.IsEnabled)
                .ToListAsync();

            // Standard prices first, then each category / subcategory price list.
            return offers
                .OrderBy(o => SponsoredListingOfferPlacement.SortKey(o))
                .ThenBy(o => o.Days)
                .ToList();
        }

        public async Task<IEnumerable<SponsoredListingOffer>> GetOffersForPlacementAsync(SponsorshipType sponsorshipType, int? placementId)
        {
            var enabled = this.context.SponsoredListingOffers
                .Where(x => x.SponsorshipType == sponsorshipType && x.IsEnabled);

            if (placementId is int id && id > 0)
            {
                List<SponsoredListingOffer> own = sponsorshipType switch
                {
                    SponsorshipType.CategorySponsor => await enabled
                        .Where(x => x.CategoryId == id && x.SubcategoryId == null)
                        .ToListAsync(),
                    SponsorshipType.SubcategorySponsor => await enabled
                        .Where(x => x.SubcategoryId == id && x.CategoryId == null)
                        .ToListAsync(),
                    _ => new List<SponsoredListingOffer>(),
                };

                if (own.Count > 0)
                {
                    return own.OrderBy(o => o.Days).ToList();
                }
            }

            return await enabled
                .Where(x => x.CategoryId == null && x.SubcategoryId == null)
                .OrderBy(o => o.Days)
                .ToListAsync();
        }

        public async Task<SponsoredListingOffer> GetByIdAsync(int sponsoredListingOfferId)
        {
            var result = await this.context.SponsoredListingOffers.FindAsync(sponsoredListingOfferId);

            return result ?? throw new Exception("Offer not found");
        }

        public async Task CreateAsync(SponsoredListingOffer offer)
        {
            try
            {
                await this.context.SponsoredListingOffers.AddAsync(offer);
                await this.context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task UpdateAsync(SponsoredListingOffer offer)
        {
            try
            {
                this.context.SponsoredListingOffers.Update(offer);
                await this.context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task DeleteOfferAsync(int sponsoredListingOfferId)
        {
            var offer = await this.GetByIdAsync(sponsoredListingOfferId);
            if (offer != null)
            {
                this.context.SponsoredListingOffers.Remove(offer);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<DateTime?> GetLastModifiedDateAsync()
        {
            return await this.context.SponsoredListingOffers
                .Where(o => o.IsEnabled)
                .Select(o =>
                    o.UpdateDate.HasValue && o.UpdateDate > o.CreateDate
                        ? o.UpdateDate.Value
                        : o.CreateDate)
                .MaxAsync();
        }
    }
}