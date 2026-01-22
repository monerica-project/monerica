using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

public class DirectoryFilterController : Controller
{
    private readonly IDirectoryEntryRepository entryRepo;
    private readonly ICategoryRepository categoryRepo;
    private readonly ISubcategoryRepository subcategoryRepo;
    private readonly ICacheService cacheService;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly IMemoryCache memoryCache;

    public DirectoryFilterController(
        IDirectoryEntryRepository entryRepo,
        ICategoryRepository categoryRepo,
        ISubcategoryRepository subcategoryRepo,
        ICacheService cacheService,
        ISponsoredListingRepository sponsoredListingRepository,
        IMemoryCache memoryCache)
    {
        this.entryRepo = entryRepo;
        this.categoryRepo = categoryRepo;
        this.subcategoryRepo = subcategoryRepo;
        this.cacheService = cacheService;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.memoryCache = memoryCache;
    }

    [HttpGet("filter")]
    public async Task<IActionResult> Index([FromQuery] DirectoryFilterQuery q)
    {
        q ??= new DirectoryFilterQuery();

        // enforce 10 per page
        q.PageSize = 10;
        if (q.Page < 1)
        {
            q.Page = 1;
        }

        // Default statuses if none selected
        if (q.Statuses is null || q.Statuses.Count == 0)
        {
            q.Statuses = new List<DirectoryStatus> { DirectoryStatus.Admitted, DirectoryStatus.Verified };
        }

        var result = await this.entryRepo.FilterAsync(q);

        var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
        var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

        var vmList = ViewModelConverter.ConvertToViewModels(
            result.Items.ToList(),
            DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
            DirectoryManager.DisplayFormatting.Enums.ItemDisplayType.SearchResult,
            link2Name,
            link3Name);

        // Sponsor pinning
        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        foreach (var item in vmList)
        {
            item.IsSponsored = allSponsors.Any(x => x.DirectoryEntryId == item.DirectoryEntryId);
        }
        vmList = vmList.Where(e => e.IsSponsored).Concat(vmList.Where(e => !e.IsSponsored)).ToList();

        // -------------------------
        // Options: Countries (cached)
        // -------------------------
        var countryOptions = await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCountriesCacheName,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                entry.SlidingExpiration = TimeSpan.FromHours(1);

                var existingCodes = await this.entryRepo.ListActiveCountryCodesAsync();
                var allCountries = DirectoryManager.Utilities.Helpers.CountryHelper.GetCountries(); // ISO2 -> name

                return existingCodes
                    .Where(code => allCountries.ContainsKey(code))
                    .Select(code => (Code: code, Name: allCountries[code]))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

        // -------------------------
        // Options: Active Categories (cached)
        // -------------------------
        var categoryOptions = await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCategoriesCacheName,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                entry.SlidingExpiration = TimeSpan.FromHours(1);

                var activeCategories = await this.categoryRepo.GetActiveCategoriesAsync();

                return activeCategories
                    .Select(c => new IdNameOption { Id = c.CategoryId, Name = c.Name })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

        // -------------------------
        // Options: Active Subcategories for selected category (cached per categoryId)
        // -------------------------
        var subCategoryOptions = new List<IdNameOption>();

        if (q.CategoryId is > 0)
        {
            int catId = q.CategoryId.Value;
            string subcatCacheKey = StringConstants.ActiveSubcategoriesByCategoryCachePrefix + catId;

            subCategoryOptions = await this.memoryCache.GetOrCreateAsync(
                subcatCacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                    entry.SlidingExpiration = TimeSpan.FromHours(1);

                    var activeSubcats = await this.subcategoryRepo.GetActiveSubcategoriesAsync(catId);

                    return activeSubcats
                        .Select(sc => new IdNameOption { Id = sc.SubCategoryId, Name = sc.Name })
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }) ?? new List<IdNameOption>();
        }

        var vm = new DirectoryFilterViewModel
        {
            Query = q,
            TotalCount = result.TotalCount,
            Entries = vmList,

            CountryOptions = countryOptions,
            CategoryOptions = categoryOptions,
            SubCategoryOptions = subCategoryOptions,

            // if you want only these 4, supply them in this order instead of Enum.GetValues:
            AllStatuses = new List<DirectoryStatus>
            {
                DirectoryStatus.Verified,
                DirectoryStatus.Admitted,
                DirectoryStatus.Questionable,
                DirectoryStatus.Scam
            }
        };

        this.ViewData["Title"] = "Directory Filter";
        return this.View(vm);
    }
}
