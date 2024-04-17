using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
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
            return await this.context.SponsoredListingOffers.ToListAsync();
        }

        public async Task<IEnumerable<SponsoredListingOffer>> GetAllByTypeAsync(SponsorshipType sponsorshipType)
        {
            return await this.context
                             .SponsoredListingOffers
                             .Where(x => x.SponsorshipType == sponsorshipType && x.IsEnabled == true).ToListAsync();
        }

        public async Task<SponsoredListingOffer> GetByIdAsync(int sponsoredListingOfferId)
        {
            var result = await this.context.SponsoredListingOffers.FindAsync(sponsoredListingOfferId);

            return result ?? throw new Exception("Offer not found");
        }

        public async Task CreateAsync(SponsoredListingOffer offer)
        {
            await this.context.SponsoredListingOffers.AddAsync(offer);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SponsoredListingOffer offer)
        {
            this.context.SponsoredListingOffers.Update(offer);
            await this.context.SaveChangesAsync();
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
    }
}