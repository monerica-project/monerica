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

public class SearchController : Controller
{
    private readonly IDirectoryEntryRepository entryRepo;
    private readonly ICacheService cacheService;
    private readonly ISearchLogRepository searchLogRepository;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly ISearchBlacklistRepository blacklistRepository;
    private readonly IMemoryCache memoryCache;
    private readonly IUrlResolutionService urlResolver; // optional but nice

    public SearchController(
        IDirectoryEntryRepository entryRepo,
        ICacheService cacheService,
        ISearchLogRepository searchLogRepository,
        ISponsoredListingRepository sponsoredListingRepository,
        ISearchBlacklistRepository blacklistRepository,
        IMemoryCache memoryCache,
        IUrlResolutionService urlResolver)
    {
        this.entryRepo = entryRepo;
        this.cacheService = cacheService;
        this.searchLogRepository = searchLogRepository;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.blacklistRepository = blacklistRepository;
        this.memoryCache = memoryCache;
        this.urlResolver = urlResolver;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return this.BadRequest("No search performed.");
        }

        // 🔒 Blacklist check (case-insensitive contains)
        var qNorm = q.Trim().ToLowerInvariant();
        var black = (await this.GetBlackTermsAsync())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLowerInvariant())
                    .ToArray();

        string? hit = black.FirstOrDefault(term =>
            System.Text.RegularExpressions.Regex.IsMatch(
                qNorm, $@"\b{System.Text.RegularExpressions.Regex.Escape(term)}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant));

        if (hit is not null)
        {
            var blackListUrl = this.cacheService.GetSnippet(SiteConfigSetting.SearchBlacklistRedirectUrl);
            return !string.IsNullOrWhiteSpace(blackListUrl)
                ? this.Redirect(blackListUrl)
                : this.NotFound();
        }

        // normal flow below ...
        var result = await this.entryRepo.SearchAsync(q, page, pageSize);
        var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
        var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

        var vmList = ViewModelConverter.ConvertToViewModels(
            result.Items.ToList(),
            DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
            DirectoryManager.DisplayFormatting.Enums.ItemDisplayType.SearchResult,
            link2Name,
            link3Name);

        await this.searchLogRepository.CreateAsync(new SearchLog
        {
            Term = q.Trim(),
            IpAddress = this.HttpContext.GetRemoteIpIfEnabled()
        });

        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        foreach (var item in vmList)
        {
            item.IsSponsored = allSponsors.Any(x => x.DirectoryEntryId == item.DirectoryEntryId);
        }

        vmList = vmList.Where(e => e.IsSponsored).Concat(vmList.Where(e => !e.IsSponsored)).ToList();

        var vm = new SearchViewModel
        {
            Query = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            Entries = vmList
        };

        return this.View(vm);
    }

    private async Task<HashSet<string>> GetBlackTermsAsync()
    {
        if (this.memoryCache.TryGetValue(StringConstants.CacheKeySearchBlacklistTerms, out HashSet<string>? set) && set is not null)
        {
            return set;
        }

        var terms = await this.blacklistRepository.GetAllTermsAsync();
        var norm = new HashSet<string>(terms
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant()));

        _ = this.memoryCache.Set(
            StringConstants.CacheKeySearchBlacklistTerms, norm, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });

        return norm;
    }
}