using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Caches (10 minutes) the set of category and subcategory pages that have at least one
    /// listing able to buy a sponsorship there, using the same rule checkout enforces.
    /// </summary>
    public sealed class SponsorableScopeService : ISponsorableScopeService
    {
        private const string CacheKey = "sponsorable-scopes";
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

        private readonly IMemoryCache cache;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly IDirectoryEntryAdditionalSubcategoryRepository additionalRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly ISponsoredListingRepository sponsoredListingRepo;

        public SponsorableScopeService(
            IMemoryCache cache,
            IDirectoryEntryRepository entryRepo,
            IDirectoryEntryAdditionalSubcategoryRepository additionalRepo,
            ISubcategoryRepository subcategoryRepo,
            ISponsoredListingRepository sponsoredListingRepo)
        {
            this.cache = cache;
            this.entryRepo = entryRepo;
            this.additionalRepo = additionalRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.sponsoredListingRepo = sponsoredListingRepo;
        }

        public async Task<bool> CategoryHasBuyerAsync(int categoryId) =>
            categoryId > 0 && (await this.GetAsync().ConfigureAwait(false)).Categories.Contains(categoryId);

        public async Task<bool> SubcategoryHasBuyerAsync(int subCategoryId) =>
            subCategoryId > 0 && (await this.GetAsync().ConfigureAwait(false)).Subcategories.Contains(subCategoryId);

        /// <summary>
        /// Pure computation (unit-tested). A listing counts for:
        ///  - its main subcategory and that subcategory's category,
        ///  - each additional subcategory it is shown in (subcategory sponsorship only),
        /// when it can buy (<see cref="SponsorshipEligibilityHelper.CanBuy"/>) and the subcategory it
        /// would buy through doesn't have sponsorship switched off.
        /// </summary>
        public static SponsorableScopes Compute(
            IEnumerable<DirectoryEntry> advertisers,
            IEnumerable<(int DirectoryEntryId, int SubCategoryId)> additionalPairs,
            IReadOnlyDictionary<int, Subcategory> subcategoriesById,
            ISet<int> activeSponsorEntryIds,
            int minimumDaysListed,
            DateTime nowUtc)
        {
            var subs = new HashSet<int>();
            var cats = new HashSet<int>();
            var buyers = new HashSet<int>();

            foreach (var e in advertisers)
            {
                if (!SponsorshipEligibilityHelper.CanBuy(
                        e.DirectoryStatus, e.CreateDate, activeSponsorEntryIds.Contains(e.DirectoryEntryId), minimumDaysListed, nowUtc))
                {
                    continue;
                }

                buyers.Add(e.DirectoryEntryId);

                if (!subcategoriesById.TryGetValue(e.SubCategoryId, out var main) || main.SponsorshipDisabled)
                {
                    continue; // checkout refuses any placement for a listing in a blocked subcategory
                }

                subs.Add(main.SubCategoryId);
                cats.Add(main.CategoryId);
            }

            foreach (var (entryId, subId) in additionalPairs)
            {
                if (buyers.Contains(entryId)
                    && subcategoriesById.TryGetValue(subId, out var alt)
                    && !alt.SponsorshipDisabled)
                {
                    subs.Add(subId);
                }
            }

            return new SponsorableScopes(cats, subs);
        }

        private async Task<SponsorableScopes> GetAsync()
        {
            if (this.cache.TryGetValue(CacheKey, out SponsorableScopes? cached) && cached != null)
            {
                return cached;
            }

            var advertisers = await this.entryRepo.GetAllowableAdvertisers().ConfigureAwait(false);
            var pairs = await this.additionalRepo.GetAllPairsAsync().ConfigureAwait(false);
            var subcategories = (await this.subcategoryRepo.GetAllAsync().ConfigureAwait(false))
                .ToDictionary(s => s.SubCategoryId);
            var active = await this.sponsoredListingRepo.GetActivelySponsoredDirectoryEntryIdsAsync().ConfigureAwait(false);

            var scopes = Compute(
                advertisers,
                pairs,
                subcategories,
                active,
                DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring,
                DateTime.UtcNow);

            this.cache.Set(CacheKey, scopes, CacheFor);
            return scopes;
        }
    }

    public sealed record SponsorableScopes(IReadOnlySet<int> Categories, IReadOnlySet<int> Subcategories);
}
