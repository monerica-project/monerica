using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
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

        public async Task<IEnumerable<SponsoredListingOffer>> GetAllOffersAsync()
        {
            return await this.context.SponsoredListingOffers.ToListAsync();
        }

        public async Task<SponsoredListingOffer> GetOfferByIdAsync(int id)
        {
            var result = await this.context.SponsoredListingOffers.FindAsync(id);

            return result ?? throw new Exception("Offer not found");
        }

        public async Task AddOfferAsync(SponsoredListingOffer offer)
        {
            await this.context.SponsoredListingOffers.AddAsync(offer);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateOfferAsync(SponsoredListingOffer offer)
        {
            this.context.SponsoredListingOffers.Update(offer);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteOfferAsync(int id)
        {
            var offer = await this.GetOfferByIdAsync(id);
            if (offer != null)
            {
                this.context.SponsoredListingOffers.Remove(offer);
                await this.context.SaveChangesAsync();
            }
        }
    }
}