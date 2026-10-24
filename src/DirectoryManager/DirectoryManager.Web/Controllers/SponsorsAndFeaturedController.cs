using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Public page listing every paid sponsor and every unpaid "Featured" pick, so it is clear
    /// which is which. A paid sponsor in a spot always overrides that spot's featured pick.
    /// </summary>
    [AllowAnonymous]
    public class SponsorsAndFeaturedController : BaseController
    {
        public const string PagePath = "sponsors-and-featured";

        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICacheService cacheService;

        public SponsorsAndFeaturedController(
            ISponsoredListingRepository sponsoredListingRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService,
            IMemoryCache cache)
            : base(cache)
        {
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheService = cacheService;
        }

        [HttpGet(PagePath)]
        public async Task<IActionResult> Index()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.PageMeta().CanonicalUrl = UrlBuilder.CombineUrl(canonicalDomain, PagePath);

            var mainSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            var categorySponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);
            var subcategorySponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);

            var categories = (await this.categoryRepository.GetActiveCategoriesAsync())
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Name)
                .ToList();
            var subcategories = (await this.subcategoryRepository.GetAllActiveSubCategoriesAsync())
                .Where(s => s.IsEnabled)
                .ToList();

            // Featured picks (only Admitted/Verified listings are shown).
            var featuredIds = categories.Select(c => c.FeaturedDirectoryEntryId)
                .Concat(subcategories.Select(s => s.FeaturedDirectoryEntryId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var featuredEntries = (await this.directoryEntryRepository.GetByIdsAsync(featuredIds))
                .Where(kv => FeaturedListingRules.IsFeaturable(kv.Value.DirectoryStatus))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // Spot of each paid sponsorship: the category/subcategory it was bought for.
            var subToCategory = subcategories.ToDictionary(s => s.SubCategoryId, s => s.CategoryId);
            var categorySponsorsById = categorySponsors
                .GroupBy(s => s.CategoryId ?? s.DirectoryEntry?.SubCategory?.CategoryId ?? 0)
                .ToDictionary(g => g.Key, g => g.ToList());
            var subcategorySponsorsById = subcategorySponsors
                .GroupBy(s => s.SubCategoryId ?? s.DirectoryEntry?.SubCategoryId ?? 0)
                .ToDictionary(g => g.Key, g => g.ToList());

            var vm = new SponsorsAndFeaturedViewModel
            {
                SiteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName),
                // Same order as the homepage: the sponsorship ending furthest in the future first.
                MainSponsors = mainSponsors
                    .Where(s => s.DirectoryEntry != null)
                    .OrderByDescending(s => s.CampaignEndDate)
                    .ThenByDescending(s => s.CampaignStartDate)
                    .Select(s => ToListing(s.DirectoryEntry!))
                    .DistinctBy(l => l.ProfilePath)
                    .ToList(),
            };

            foreach (var category in categories)
            {
                var group = new SpotCategoryGroup
                {
                    CategoryName = category.Name,
                    CategoryPath = "/" + category.CategoryKey,
                };

                var categoryRow = BuildRow(
                    "Category page",
                    group.CategoryPath,
                    isCategorySpot: true,
                    categorySponsorsById.GetValueOrDefault(category.CategoryId),
                    category.FeaturedDirectoryEntryId,
                    featuredEntries);
                if (categoryRow != null)
                {
                    group.Rows.Add(categoryRow);
                }

                foreach (var sub in subcategories.Where(s => s.CategoryId == category.CategoryId).OrderBy(s => s.Name))
                {
                    var row = BuildRow(
                        sub.Name,
                        $"/{category.CategoryKey}/{sub.SubCategoryKey}",
                        isCategorySpot: false,
                        subcategorySponsorsById.GetValueOrDefault(sub.SubCategoryId),
                        sub.FeaturedDirectoryEntryId,
                        featuredEntries);
                    if (row != null)
                    {
                        group.Rows.Add(row);
                    }
                }

                if (group.Rows.Count > 0)
                {
                    vm.Categories.Add(group);
                }
            }

            var rows = vm.Categories.SelectMany(c => c.Rows).ToList();
            vm.SponsoredSpotCount = rows.Count(r => r.IsSponsored);
            vm.FeaturedSpotCount = rows.Count(r => !r.IsSponsored);

            return this.View("SponsorsAndFeatured", vm);
        }

        /// <summary>
        /// A spot shows its paid sponsor(s) when there are any (they override the featured pick),
        /// otherwise its featured pick, otherwise nothing (null).
        /// </summary>
        private static SpotRow? BuildRow(
            string spotName,
            string spotPath,
            bool isCategorySpot,
            List<SponsoredListing>? paid,
            int? featuredId,
            IReadOnlyDictionary<int, DirectoryEntry> featuredEntries)
        {
            var paidListings = (paid ?? new List<SponsoredListing>())
                .Where(s => s.DirectoryEntry != null)
                .Select(s => ToListing(s.DirectoryEntry!))
                .DistinctBy(l => l.ProfilePath)
                .ToList();

            if (paidListings.Count > 0)
            {
                return new SpotRow { SpotName = spotName, SpotPath = spotPath, IsCategorySpot = isCategorySpot, IsSponsored = true, Listings = paidListings };
            }

            if (featuredId.HasValue && featuredEntries.TryGetValue(featuredId.Value, out var entry))
            {
                return new SpotRow { SpotName = spotName, SpotPath = spotPath, IsCategorySpot = isCategorySpot, IsSponsored = false, Listings = { ToListing(entry) } };
            }

            return null;
        }

        private static SpotListing ToListing(DirectoryEntry e) => new ()
        {
            Name = e.Name,
            ProfilePath = UrlBuilder.ListingPath(e.DirectoryEntryKey),
            DirectoryStatus = e.DirectoryStatus,
        };
    }
}
