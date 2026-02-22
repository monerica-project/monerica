using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

public class DirectoryFilterController : Controller
{
    private const int PageSize = 10;

    // cache durations (same everywhere)
    private static readonly TimeSpan CacheAbsolute = TimeSpan.FromHours(6);
    private static readonly TimeSpan CacheSliding = TimeSpan.FromHours(1);

    private readonly IDirectoryEntryRepository entryRepo;
    private readonly IDirectoryEntryReviewRepository entryReviewsRepo;
    private readonly ICategoryRepository categoryRepo;
    private readonly ISubcategoryRepository subcategoryRepo;
    private readonly ICacheService cacheService;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly IMemoryCache memoryCache;
    private readonly ITagRepository tagRepo;

    public DirectoryFilterController(
        IDirectoryEntryRepository entryRepo,
        IDirectoryEntryReviewRepository entryReviewsRepo,
        ICategoryRepository categoryRepo,
        ISubcategoryRepository subcategoryRepo,
        ITagRepository tagRepo,
        ICacheService cacheService,
        ISponsoredListingRepository sponsoredListingRepository,
        IMemoryCache memoryCache)
    {
        this.entryRepo = entryRepo;
        this.entryReviewsRepo = entryReviewsRepo;
        this.categoryRepo = categoryRepo;
        this.subcategoryRepo = subcategoryRepo;
        this.tagRepo = tagRepo;
        this.cacheService = cacheService;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.memoryCache = memoryCache;
    }

    [HttpGet("filter")]
    public async Task<IActionResult> Index([FromQuery] DirectoryFilterQuery q)
    {
        q ??= new DirectoryFilterQuery();

        NormalizeQuery(q);

        // wipe / sanitize tag filters if they don't match the selected category
        await this.SanitizeTagIdsForCategoryAsync(q);

        // core results
        var result = await this.entryRepo.FilterAsync(q);

        // link labels (used in both view models + sponsor models)
        var (link2Name, link3Name) = await this.GetLinkLabelsAsync();

        // convert result items into VMs
        var vmList = ConvertItemsToViewModels(result, link2Name, link3Name);

        // rating summaries for this page only
        await this.ApplyRatingsAsync(vmList);

        // sponsor pinning + order sponsors first
        vmList = await this.ApplySponsorsAndOrderAsync(vmList);

        // dropdown options
        var countryOptions = await this.GetCountryOptionsAsync();
        var categoryOptions = await this.GetCategoryOptionsAsync();
        var categoryTagOptions = await this.GetCategoryTagOptionsAsync(q.CategoryId);
        var subCategoryOptions = await this.GetSubCategoryOptionsAsync(q.CategoryId);

        // sponsor block models for the filter page
        var (categorySponsorModel, subcategorySponsorModel) =
            await this.GetSponsorModelsAsync(q, link2Name, link3Name);

        var vm = BuildViewModel(
            q,
            result.TotalCount,
            vmList,
            countryOptions,
            categoryOptions,
            subCategoryOptions,
            categoryTagOptions,
            categorySponsorModel,
            subcategorySponsorModel);

        this.ViewData[StringConstants.TitleHeader] = "Directory Filter";
        return this.View(vm);
    }

    // -------------------------
    // Query normalization
    // -------------------------
    private static void NormalizeQuery(DirectoryFilterQuery q)
    {
        q.PageSize = PageSize;

        if (q.Page < 1)
        {
            q.Page = 1;
        }

        // ✅ NEW: if querystring has an invalid Sort value, fall back safely
        if (!Enum.IsDefined(typeof(DirectoryFilterSort), q.Sort))
        {
            q.Sort = DirectoryFilterSort.Newest;
            q.Page = 1;
        }

        // Default statuses if none selected
        if (q.Statuses is null || q.Statuses.Count == 0)
        {
            q.Statuses = new List<DirectoryStatus>
        {
            DirectoryStatus.Admitted,
            DirectoryStatus.Verified
        };
        }
    }

    // -------------------------
    // Snippets
    // -------------------------
    private async Task<(string link2Name, string link3Name)> GetLinkLabelsAsync()
    {
        var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
        var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
        return (link2Name, link3Name);
    }

    // -------------------------
    // View model conversion
    // -------------------------
    private static List<DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel> ConvertItemsToViewModels(
        PagedResult<DirectoryEntry> result,
        string link2Name,
        string link3Name)
    {
        return ViewModelConverter.ConvertToViewModels(
            result.Items.ToList(),
            DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
            DirectoryManager.DisplayFormatting.Enums.ItemDisplayType.SearchResult,
            link2Name,
            link3Name);
    }

    // -------------------------
    // Ratings
    // -------------------------
    private async Task ApplyRatingsAsync(List<DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel> items)
    {
        var ids = items.Select(x => x.DirectoryEntryId).Distinct().ToList();
        var ratingMap = await this.entryReviewsRepo.GetRatingSummariesAsync(ids);

        foreach (var item in items)
        {
            if (ratingMap.TryGetValue(item.DirectoryEntryId, out var rs) && rs.ReviewCount > 0)
            {
                item.AverageRating = rs.AvgRating;
                item.ReviewCount = rs.ReviewCount;
            }
            else
            {
                item.AverageRating = null;
                item.ReviewCount = 0;
            }
        }
    }

    // -------------------------
    // Sponsors (pin + order)
    // -------------------------
    private async Task<List<DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel>> ApplySponsorsAndOrderAsync(
        List<DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel> items)
    {
        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        var sponsorIds = new HashSet<int>(allSponsors.Select(s => s.DirectoryEntryId));

        foreach (var item in items)
        {
            item.IsSponsored = sponsorIds.Contains(item.DirectoryEntryId);
        }

        return items.Where(e => e.IsSponsored).Concat(items.Where(e => !e.IsSponsored)).ToList();
    }

    // -------------------------
    // Options: Countries (cached)
    // -------------------------
    private async Task<List<(string Code, string Name)>> GetCountryOptionsAsync()
    {
        return await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCountriesCacheName,
            async cacheEntry =>
            {
                SetCachePolicy(cacheEntry, StringConstants.PrefixDirectoryFilter);

                var existingCodes = await this.entryRepo.ListActiveCountryCodesAsync();
                var allCountries = DirectoryManager.Utilities.Helpers.CountryHelper.GetCountries(); // ISO2 -> name

                return existingCodes
                    .Where(code => allCountries.ContainsKey(code))
                    .Select(code => (Code: code, Name: allCountries[code]))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<(string Code, string Name)>();
    }

    // -------------------------
    // Options: Categories (cached)
    // -------------------------
    private async Task<List<IdNameOption>> GetCategoryOptionsAsync()
    {
        return await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCategoriesCacheName,
            async cacheEntry =>
            {
                SetCachePolicy(cacheEntry, StringConstants.PrefixActiveCategories);

                var activeCategories = await this.categoryRepo.GetActiveCategoriesAsync();

                return activeCategories
                    .Select(c => new IdNameOption { Id = c.CategoryId, Name = c.Name })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<IdNameOption>();
    }

    // -------------------------
    // Options: Tags in selected category (cached per categoryId)
    // -------------------------
    private async Task<List<IdNameOption>> GetCategoryTagOptionsAsync(int? categoryId)
    {
        if (!(categoryId is > 0))
        {
            return new List<IdNameOption>();
        }

        int catId = categoryId.Value;
        string tagCacheKey = StringConstants.ActiveTagsByCategoryCachePrefix + catId;

        return await this.memoryCache.GetOrCreateAsync(
            tagCacheKey,
            async cacheEntry =>
            {
                SetCachePolicy(cacheEntry, StringConstants.PrefixActiveTagsByCat);

                var tags = await this.tagRepo.ListTagsForCategoryAsync(catId);

                return tags
                    .Select(t => new IdNameOption { Id = t.TagId, Name = t.Name })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<IdNameOption>();
    }

    // -------------------------
    // Options: Subcategories for selected category (cached per categoryId)
    // -------------------------
    private async Task<List<IdNameOption>> GetSubCategoryOptionsAsync(int? categoryId)
    {
        if (!(categoryId is > 0))
        {
            return new List<IdNameOption>();
        }

        int catId = categoryId.Value;
        string subcatCacheKey = StringConstants.ActiveSubcategoriesByCategoryCachePrefix + catId;

        return await this.memoryCache.GetOrCreateAsync(
            subcatCacheKey,
            async cacheEntry =>
            {
                SetCachePolicy(cacheEntry, StringConstants.PrefixActiveSubcats);

                var activeSubcats = await this.subcategoryRepo.GetActiveSubcategoriesAsync(catId);

                return activeSubcats
                    .Select(sc => new IdNameOption { Id = sc.SubCategoryId, Name = sc.Name })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<IdNameOption>();
    }

    // -------------------------
    // Sponsor models for filter page (exact same logic, just extracted)
    // -------------------------
    private async Task<(CategorySponsorModel? category, SubcategorySponsorModel? subcategory)> GetSponsorModelsAsync(
        DirectoryFilterQuery q,
        string link2Name,
        string link3Name)
    {
        CategorySponsorModel? categorySponsorModel = null;
        SubcategorySponsorModel? subcategorySponsorModel = null;

        if (q.SubCategoryId is > 0)
        {
            int subId = q.SubCategoryId.Value;

            var subCategorySponsors = await this.sponsoredListingRepository.GetSponsoredListingsForSubCategory(subId);
            var subSponsor = subCategorySponsors.FirstOrDefault();

            int totalActiveInSub = await this.entryRepo.CountBySubcategoryAsync(subId);

            subcategorySponsorModel = new SubcategorySponsorModel
            {
                SubCategoryId = subId,
                TotalActiveSubCategoryListings = totalActiveInSub,
                DirectoryEntry = (subSponsor?.DirectoryEntry != null)
                    ? BuildSponsorEntryVm(subSponsor.DirectoryEntry, link2Name, link3Name)
                    : null
            };
        }
        else if (q.CategoryId is > 0)
        {
            int catId = q.CategoryId.Value;

            var categorySponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);

            var catSponsor = categorySponsors.FirstOrDefault(s =>
                (s.CategoryId.HasValue && s.CategoryId.Value == catId) ||
                (s.DirectoryEntry?.SubCategory?.CategoryId == catId));

            int totalActiveInCat = await this.entryRepo.CountByCategoryAsync(catId);

            categorySponsorModel = new CategorySponsorModel
            {
                CategoryId = catId,
                TotalActiveCategoryListings = totalActiveInCat,
                DirectoryEntry = (catSponsor?.DirectoryEntry != null)
                    ? BuildSponsorEntryVm(catSponsor.DirectoryEntry, link2Name, link3Name)
                    : null
            };
        }

        return (categorySponsorModel, subcategorySponsorModel);
    }

    private static DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel BuildSponsorEntryVm(
        DirectoryEntry e,
        string link2Name,
        string link3Name)
    {
        // IMPORTANT: match your real route
        // If your profile route is different, change this one line.
        string itemPath = DirectoryManager.DisplayFormatting.Helpers.FormattingHelper.ListingPath(e.DirectoryEntryKey);

        return new DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel
        {
            DateOption = DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,

            // Sponsors on filter page should link to profile so (count) -> #reviews works
            LinkType = DirectoryManager.DisplayFormatting.Enums.LinkType.ListingPage,
            ItemPath = itemPath,

            // ✅ needed for /site/{key}
            DirectoryEntryKey = e.DirectoryEntryKey,

            // dates
            CreateDate = e.CreateDate,
            UpdateDate = e.UpdateDate,

            // links (+ affiliate variants if your partial expects them)
            Link = e.Link,
            LinkA = e.LinkA,

            Link2 = e.Link2,
            Link2A = e.Link2A,

            Link3 = e.Link3,
            Link3A = e.Link3A,

            Link2Name = link2Name,
            Link3Name = link3Name,

            // main fields
            Name = e.Name,
            Contact = e.Contact,
            Description = e.Description,

            DirectoryEntryId = e.DirectoryEntryId,
            DirectoryStatus = e.DirectoryStatus,
            DirectoryBadge = e.DirectoryBadge,

            CountryCode = e.CountryCode,
            Location = e.Location,
            Note = e.Note,
            Processor = e.Processor,

            // keep SubCategory if you want breadcrumbs etc in some render modes
            SubCategory = e.SubCategory,
            SubCategoryId = e.SubCategoryId,

            // sponsor flags
            IsSponsored = true,
            DisplayAsSponsoredItem = false
        };
    }

    // -------------------------
    // Cache helper (NOW adds prefix invalidation token)
    // -------------------------
    private static void SetCachePolicy(ICacheEntry cacheEntry, string prefix)
    {
        cacheEntry.AbsoluteExpirationRelativeToNow = CacheAbsolute;
        cacheEntry.SlidingExpiration = CacheSliding;
        cacheEntry.AddExpirationToken(CachePrefixManager.GetToken(prefix));
    }

    // -------------------------
    // View model creation
    // -------------------------
    private static DirectoryFilterViewModel BuildViewModel(
        DirectoryFilterQuery q,
        int totalCount,
        List<DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel> entries,
        List<(string Code, string Name)> countryOptions,
        List<IdNameOption> categoryOptions,
        List<IdNameOption> subCategoryOptions,
        List<IdNameOption> categoryTagOptions,
        CategorySponsorModel? categorySponsorModel,
        SubcategorySponsorModel? subcategorySponsorModel)
    {
        return new DirectoryFilterViewModel
        {
            Query = q,
            TotalCount = totalCount,
            Entries = entries,

            CountryOptions = countryOptions,
            CategoryOptions = categoryOptions,
            SubCategoryOptions = subCategoryOptions,

            CategoryTagOptions = categoryTagOptions,

            CategorySponsorModel = categorySponsorModel,
            SubcategorySponsorModel = subcategorySponsorModel,

            AllStatuses = new List<DirectoryStatus>
            {
                DirectoryStatus.Verified,
                DirectoryStatus.Admitted,
                DirectoryStatus.Questionable,
                DirectoryStatus.Scam
            }
        };
    }

    // -------------------------
    // Tag sanitizer (exact same logic)
    // -------------------------
    private async Task SanitizeTagIdsForCategoryAsync(DirectoryFilterQuery q)
    {
        if (!(q.CategoryId is > 0))
        {
            q.TagIds = new List<int>();
            return;
        }

        if (q.TagIds is null || q.TagIds.Count == 0)
        {
            return;
        }

        int catId = q.CategoryId.Value;
        string idCacheKey = StringConstants.ActiveTagIdsByCategoryCachePrefix + catId;

        var allowedIds = await this.memoryCache.GetOrCreateAsync(
            idCacheKey,
            async cacheEntry =>
            {
                SetCachePolicy(cacheEntry, StringConstants.PrefixActiveTagIdsByCat);

                var tags = await this.tagRepo.ListTagsForCategoryAsync(catId);
                return tags.Select(t => t.TagId).ToHashSet();
            }) ?? new HashSet<int>();

        var cleaned = q.TagIds
            .Where(id => id > 0 && allowedIds.Contains(id))
            .Distinct()
            .ToList();

        if (cleaned.Count != q.TagIds.Count)
        {
            q.TagIds = cleaned;
            q.Page = 1;
        }
    }
}
