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
    private readonly ICacheService cacheService;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly IMemoryCache memoryCache;

    public DirectoryFilterController(
        IDirectoryEntryRepository entryRepo,
        ICacheService cacheService,
        ISponsoredListingRepository sponsoredListingRepository,
        IMemoryCache memoryCache)
    {
        this.entryRepo = entryRepo;
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

        // Sponsor pinning (same behavior as Search)
        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        foreach (var item in vmList)
        {
            item.IsSponsored = allSponsors.Any(x => x.DirectoryEntryId == item.DirectoryEntryId);
        }

        vmList = vmList.Where(e => e.IsSponsored).Concat(vmList.Where(e => !e.IsSponsored)).ToList();

        // Options
        var countryOptions = await this.memoryCache.GetOrCreateAsync(
            StringConstants.ActiveCountriesCacheName,
            async entry =>
            {
                // Cache settings
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                entry.SlidingExpiration = TimeSpan.FromHours(1);

                // 1) Fetch only codes that exist in DB
                var existingCodes = await this.entryRepo.ListActiveCountryCodesAsync();

                // 2) Map code -> full name from your helper dictionary
                var allCountries = DirectoryManager.Utilities.Helpers.CountryHelper.GetCountries(); // ISO2 -> name

                // 3) Only keep codes that exist and are recognized by helper
                var filtered = existingCodes
                    .Where(code => allCountries.ContainsKey(code))
                    .Select(code => (Code: code, Name: allCountries[code]))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return filtered;
            });

        var categories = await this.entryRepo.ListCategoryOptionsAsync();
        var subcats = (q.CategoryId.HasValue && q.CategoryId.Value > 0)
            ? await this.entryRepo.ListSubCategoryOptionsAsync(q.CategoryId.Value)
            : new List<IdNameOption>();

        var vm = new DirectoryFilterViewModel
        {
            Query = q,
            TotalCount = result.TotalCount,
            Entries = vmList,

            CountryOptions = countryOptions,
            CategoryOptions = categories,
            SubCategoryOptions = subcats,

            AllStatuses = Enum.GetValues(typeof(DirectoryStatus)).Cast<DirectoryStatus>().ToList()
        };

        this.ViewData["Title"] = "Directory Filter";
        return this.View(vm);
    }
}
