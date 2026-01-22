using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
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
            q.Statuses = new List<DirectoryStatus>
            {
                DirectoryStatus.Admitted,
                DirectoryStatus.Verified
            };
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
        var sponsorIds = new HashSet<int>(allSponsors.Select(s => s.DirectoryEntryId));

        foreach (var item in vmList)
        {
            item.IsSponsored = sponsorIds.Contains(item.DirectoryEntryId);
        }

        // show sponsors first
        vmList = vmList.Where(e => e.IsSponsored).Concat(vmList.Where(e => !e.IsSponsored)).ToList();

        // -------------------------
        // Options: Countries (cached)
        // -------------------------
        var countryOptions = await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCountriesCacheName,
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);

                // 1) Fetch only codes that exist in DB
                var existingCodes = await this.entryRepo.ListActiveCountryCodesAsync();

                // 2) Map code -> full name from helper dictionary
                var allCountries = DirectoryManager.Utilities.Helpers.CountryHelper.GetCountries(); // ISO2 -> name

                // 3) Keep only codes that exist + are recognized by helper
                return existingCodes
                    .Where(code => allCountries.ContainsKey(code))
                    .Select(code => (Code: code, Name: allCountries[code]))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<(string Code, string Name)>();

        // -------------------------
        // Options: Active Categories (cached)
        // -------------------------
        var categoryOptions = await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCategoriesCacheName,
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);

                var activeCategories = await this.categoryRepo.GetActiveCategoriesAsync();

                return activeCategories
                    .Select(c => new IdNameOption { Id = c.CategoryId, Name = c.Name })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }) ?? new List<IdNameOption>();

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
                async cacheEntry =>
                {
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                    cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);

                    var activeSubcats = await this.subcategoryRepo.GetActiveSubcategoriesAsync(catId);

                    return activeSubcats
                        .Select(sc => new IdNameOption { Id = sc.SubCategoryId, Name = sc.Name })
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }) ?? new List<IdNameOption>();
        }

        // -------------------------
        // Sponsor block for filter page:
        // - if SubCategoryId selected => subcategory sponsor
        // - else if CategoryId selected => category sponsor
        // -------------------------
        CategorySponsorModel? categorySponsorModel = null;
        SubcategorySponsorModel? subcategorySponsorModel = null;

        if (q.SubCategoryId is > 0)
        {
            int subId = q.SubCategoryId.Value;

            var subCategorySponsors = await this.sponsoredListingRepository.GetSponsoredListingsForSubCategory(subId);
            var subSponsor = subCategorySponsors.FirstOrDefault();

            // drives CTA inside the partial
            int totalActiveInSub = await this.entryRepo.CountBySubcategoryAsync(subId);

            subcategorySponsorModel = new SubcategorySponsorModel
            {
                SubCategoryId = subId,
                TotalActiveSubCategoryListings = totalActiveInSub,
                DirectoryEntry = (subSponsor?.DirectoryEntry != null)
                    ? new DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel
                    {
                        DateOption = DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                        LinkType = DirectoryManager.DisplayFormatting.Enums.LinkType.ListingPage,

                        CreateDate = subSponsor.DirectoryEntry.CreateDate,
                        UpdateDate = subSponsor.DirectoryEntry.UpdateDate,

                        Link = subSponsor.DirectoryEntry.Link,
                        Link2 = subSponsor.DirectoryEntry.Link2,
                        Link3 = subSponsor.DirectoryEntry.Link3,
                        Link2Name = link2Name,
                        Link3Name = link3Name,

                        Name = subSponsor.DirectoryEntry.Name,
                        Contact = subSponsor.DirectoryEntry.Contact,
                        Description = subSponsor.DirectoryEntry.Description,
                        DirectoryEntryId = subSponsor.DirectoryEntry.DirectoryEntryId,
                        DirectoryStatus = subSponsor.DirectoryEntry.DirectoryStatus,
                        Location = subSponsor.DirectoryEntry.Location,
                        Note = subSponsor.DirectoryEntry.Note,
                        Processor = subSponsor.DirectoryEntry.Processor,
                        SubCategoryId = subSponsor.DirectoryEntry.SubCategoryId,

                        IsSponsored = true,
                        DisplayAsSponsoredItem = false
                    }
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

            // drives CTA inside the partial
            int totalActiveInCat = await this.entryRepo.CountByCategoryAsync(catId);

            categorySponsorModel = new CategorySponsorModel
            {
                CategoryId = catId,
                TotalActiveCategoryListings = totalActiveInCat,
                DirectoryEntry = (catSponsor?.DirectoryEntry != null)
                    ? new DirectoryManager.DisplayFormatting.Models.DirectoryEntryViewModel
                    {
                        DateOption = DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                        LinkType = DirectoryManager.DisplayFormatting.Enums.LinkType.ListingPage,

                        CreateDate = catSponsor.DirectoryEntry.CreateDate,
                        UpdateDate = catSponsor.DirectoryEntry.UpdateDate,

                        Link = catSponsor.DirectoryEntry.Link,
                        Link2 = catSponsor.DirectoryEntry.Link2,
                        Link3 = catSponsor.DirectoryEntry.Link3,
                        Link2Name = link2Name,
                        Link3Name = link3Name,

                        Name = catSponsor.DirectoryEntry.Name,
                        Contact = catSponsor.DirectoryEntry.Contact,
                        Description = catSponsor.DirectoryEntry.Description,
                        DirectoryEntryId = catSponsor.DirectoryEntry.DirectoryEntryId,
                        DirectoryStatus = catSponsor.DirectoryEntry.DirectoryStatus,
                        Location = catSponsor.DirectoryEntry.Location,
                        Note = catSponsor.DirectoryEntry.Note,
                        Processor = catSponsor.DirectoryEntry.Processor,
                        SubCategoryId = catSponsor.DirectoryEntry.SubCategoryId,

                        IsSponsored = true,
                        DisplayAsSponsoredItem = false
                    }
                    : null
            };
        }

        var vm = new DirectoryFilterViewModel
        {
            Query = q,
            TotalCount = result.TotalCount,
            Entries = vmList,

            CountryOptions = countryOptions,
            CategoryOptions = categoryOptions,
            SubCategoryOptions = subCategoryOptions,

            CategorySponsorModel = categorySponsorModel,
            SubcategorySponsorModel = subcategorySponsorModel,

            // Only these 4 in this order
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