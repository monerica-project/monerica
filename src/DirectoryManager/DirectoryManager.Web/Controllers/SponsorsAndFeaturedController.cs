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
    /// Public /sponsors page: every paid sponsor in one place, main sponsors first, then each
    /// category's and subcategory's sponsors.
    /// </summary>
    [AllowAnonymous]
    public class SponsorsAndFeaturedController : BaseController
    {
        public const string PagePath = "sponsors";

        /// <summary>The page's old address; kept as a permanent redirect so old links still work.</summary>
        public const string OldPagePath = "sponsors-and-featured";

        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICacheService cacheService;
        private readonly IDirectoryEntryReviewRepository reviewRepository;

        public SponsorsAndFeaturedController(
            ISponsoredListingRepository sponsoredListingRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService,
            IDirectoryEntryReviewRepository reviewRepository,
            IMemoryCache cache)
            : base(cache)
        {
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheService = cacheService;
            this.reviewRepository = reviewRepository;
        }

        [HttpGet(OldPagePath)]
        public IActionResult OldAddress() => this.RedirectPermanent("/" + PagePath);

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
                    categorySponsorsById.GetValueOrDefault(category.CategoryId));
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
                        subcategorySponsorsById.GetValueOrDefault(sub.SubCategoryId));
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

            // Profile details for every sponsor: where it's listed, ratings.
            var subNames = subcategories.ToDictionary(s => s.SubCategoryId, s => s.Name);
            var catNames = categories.ToDictionary(c => c.CategoryId, c => c.Name);
            var allListings = vm.MainSponsors.Concat(vm.Categories.SelectMany(c => c.Rows).SelectMany(r => r.Listings)).ToList();
            var ratings = await this.reviewRepository.GetRatingSummariesAsync(allListings.Select(l => l.DirectoryEntryId).Distinct().ToList());
            foreach (var l in allListings)
            {
                if (l.ListedIn == null && subNames.TryGetValue(l.SubCategoryId, out var sn))
                {
                    var catId = subToCategory.GetValueOrDefault(l.SubCategoryId);
                    l.ListedIn = (catNames.TryGetValue(catId, out var cn) ? cn + " \u203a " : string.Empty) + sn;
                }

                if (ratings.TryGetValue(l.DirectoryEntryId, out var r) && r.ReviewCount > 0)
                {
                    l.AvgRating = r.AvgRating;
                    l.ReviewCount = r.ReviewCount;
                    l.VerifiedOrderCount = r.VerifiedOrderCount;
                }
            }

            var rows = vm.Categories.SelectMany(c => c.Rows).ToList();
            vm.SponsoredSpotCount = rows.Count;
            vm.FeaturedSpotCount = 0;

            return this.View("SponsorsAndFeatured", vm);
        }

        /// <summary>The paid sponsor(s) of one category or subcategory spot, or null when it has none.</summary>
        private static SpotRow? BuildRow(string spotName, string spotPath, bool isCategorySpot, List<SponsoredListing>? paid)
        {
            var paidListings = (paid ?? new List<SponsoredListing>())
                .Where(s => s.DirectoryEntry != null)
                .Select(s => ToListing(s.DirectoryEntry!))
                .DistinctBy(l => l.ProfilePath)
                .ToList();

            return paidListings.Count == 0
                ? null
                : new SpotRow { SpotName = spotName, SpotPath = spotPath, IsCategorySpot = isCategorySpot, IsSponsored = true, Listings = paidListings };
        }

        private static SpotListing ToListing(DirectoryEntry e)
        {
            var tor = new[] { e.Link2, e.Link3 }.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u) && u!.Contains(".onion", StringComparison.OrdinalIgnoreCase));
            return new SpotListing
            {
                DirectoryEntryId = e.DirectoryEntryId,
                SubCategoryId = e.SubCategoryId,
                Name = e.Name,
                ProfilePath = UrlBuilder.ListingPath(e.DirectoryEntryKey),
                DirectoryStatus = e.DirectoryStatus,
                Description = e.Description,
                CountryCode = string.IsNullOrWhiteSpace(e.CountryCode) ? null : e.CountryCode,
                CountryName = string.IsNullOrWhiteSpace(e.CountryCode) ? null : DirectoryManager.Utilities.Helpers.CountryHelper.GetCountryName(e.CountryCode),
                ListedIn = e.SubCategory?.Category != null ? $"{e.SubCategory.Category.Name} \u203a {e.SubCategory.Name}" : null,
                KycLabel = e.KycPolicy is KycPolicy k && k != KycPolicy.Unknown && k != KycPolicy.NotStated ? DirectoryManager.Utilities.Helpers.EnumHelper.GetDescription(k) : null,
                LiquidityLabel = e.Liquidity.IsPubliclyShown() ? DirectoryManager.Utilities.Helpers.EnumHelper.GetDescription(e.Liquidity) : null,
                RateLabel = e.RateType.IsShown() ? DirectoryManager.Utilities.Helpers.EnumHelper.GetDescription(e.RateType) : null,
                FoundedYear = e.FoundedDate?.Year,
                WebsiteUrl = string.IsNullOrWhiteSpace(e.Link) ? null : e.Link,
                TorUrl = tor,
            };
        }
    }
}
