using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("countries")]
public class CountriesController : Controller
{
    private const int PageSize = IntegerConstants.MaxPageSize;

    private readonly IDirectoryEntryRepository entryRepo;
    private readonly ICacheService cacheService;

    public CountriesController(IDirectoryEntryRepository entryRepo, ICacheService cacheService)
    {
        this.entryRepo = entryRepo;
        this.cacheService = cacheService;
    }

    // /countries  and  /countries/page/{page}
    [AllowAnonymous]
    [HttpGet("")]
    [HttpGet("page/{page:int}")]
    public async Task<IActionResult> All(int page = 1)
    {
        var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
        var path = page > 1 ? $"countries/page/{page}" : "countries";
        this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, path);

        var paged = await this.entryRepo.ListActiveCountriesWithCountsPagedAsync(page, PageSize);

        paged.Items = paged.Items.OrderBy(x => x.Name).ToList();

        var vm = new CountryListViewModel
        {
            PagedCountries = paged,
            CurrentPage = page,
            PageSize = PageSize
        };

        return this.View("AllCountries", vm);
    }

    // /countries/{country-slug}  and  /countries/{country-slug}/page/{page}
    [AllowAnonymous]
    [HttpGet("{countrySlug}")]
    [HttpGet("{countrySlug}/page/{page:int}")]
    public async Task<IActionResult> Index(string countrySlug, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(countrySlug))
            return this.NotFound();

        // Resolve slug -> ISO code & canonical name
        var bySlug = CountryHelper.GetCountries()
            .ToDictionary(kv => StringHelpers.UrlKey(kv.Value),
                          kv => new { Code = kv.Key, Name = kv.Value });

        var key = countrySlug.Trim().ToLowerInvariant();
        if (!bySlug.TryGetValue(key, out var info))
            return this.NotFound();

        var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
        var basePath = $"countries/{key}";
        var path = page > 1 ? $"{basePath}/page/{page}" : basePath;
        this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, path);

        var pagedRaw = await this.entryRepo.ListActiveEntriesByCountryPagedAsync(info.Code, page, PageSize);

        // link2/3 labels once
        var link2 = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
        var link3 = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

        var vms = ViewModelConverter.ConvertToViewModels(
            pagedRaw.Items.ToList(),
            DateDisplayOption.NotDisplayed,
            ItemDisplayType.Normal,
            link2,
            link3);

        var vm = new CountryEntriesViewModel
        {
            CountryCode = info.Code,
            CountryName = info.Name,
            CountryKey = key,
            PagedEntries = new PagedResult<DirectoryEntryViewModel>
            {
                Items = vms,
                TotalCount = pagedRaw.TotalCount
            },
            CurrentPage = page,
            PageSize = PageSize
        };

        return this.View("CountryEntries", vm);
    }
}
