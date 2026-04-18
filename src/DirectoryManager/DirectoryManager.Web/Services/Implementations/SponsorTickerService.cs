using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    public class SponsorTickerService : ISponsorTickerService
    {
        private readonly IMemoryCache cache;
        private readonly ISponsoredListingRepository sponsoredListingRepo;

        public SponsorTickerService(IMemoryCache cache, ISponsoredListingRepository sponsoredListingRepo)
        {
            this.cache = cache;
            this.sponsoredListingRepo = sponsoredListingRepo;
        }

        public async Task<List<SponsorTickerItemVm>> GetItemsAsync()
        {
            if (this.cache.TryGetValue(StringConstants.CacheKeySponsorTickerItems, out List<SponsorTickerItemVm> cached) && cached != null)
            {
                return cached;
            }

            var items = await this.sponsoredListingRepo.GetSponsorTickerItemsAsync();

            this.cache.Set(StringConstants.CacheKeySponsorTickerItems, items, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

            return items;
        }
    }
}
