using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

public class SearchController : Controller
{
    private readonly IDirectoryEntryRepository entryRepo;
    private readonly IDirectoryEntryReviewRepository reviewRepo; // ✅ use repository for ratings
    private readonly ICacheService cacheService;
    private readonly ISearchLogRepository searchLogRepository;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly ISearchBlacklistRepository blacklistRepository;
    private readonly IMemoryCache memoryCache;

    public SearchController(
        IDirectoryEntryRepository entryRepo,
        IDirectoryEntryReviewRepository reviewRepo,
        ICacheService cacheService,
        ISearchLogRepository searchLogRepository,
        ISponsoredListingRepository sponsoredListingRepository,
        ISearchBlacklistRepository blacklistRepository,
        IMemoryCache memoryCache,
        IUrlResolutionService urlResolver) // keep if you need it elsewhere; not used here
    {
        this.entryRepo = entryRepo;
        this.reviewRepo = reviewRepo;
        this.cacheService = cacheService;
        this.searchLogRepository = searchLogRepository;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.blacklistRepository = blacklistRepository;
        this.memoryCache = memoryCache;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return this.BadRequest("No search performed.");
        }

        if (ScriptValidation.ContainsScriptTag(q))
        {
            return this.BadRequest("Invalid search.");
        }

        // 🔒 Blacklist check (case-insensitive word-boundary match)
        var qNorm = q.Trim().ToLowerInvariant();
        var black = (await this.GetBlackTermsAsync())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .ToArray();

        string? hit = black.FirstOrDefault(term =>
            System.Text.RegularExpressions.Regex.IsMatch(
                qNorm,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(term)}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant));

        if (hit is not null)
        {
            var blackListUrl = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SearchBlacklistRedirectUrl);
            return !string.IsNullOrWhiteSpace(blackListUrl)
                ? this.Redirect(blackListUrl)
                : this.NotFound();
        }

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var result = await this.entryRepo.SearchAsync(q, page, pageSize);

        var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
        var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

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

        // Sponsor pinning
        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        var sponsorIds = new HashSet<int>(allSponsors.Select(s => s.DirectoryEntryId));

        foreach (var item in vmList)
        {
            item.IsSponsored = sponsorIds.Contains(item.DirectoryEntryId);
        }

        // ✅ Ratings for the CURRENT PAGE (approved + has value) via repository
        var pageIds = vmList.Select(v => v.DirectoryEntryId).Distinct().ToList();

        if (pageIds.Count > 0)
        {
            var ratingMap = await this.reviewRepo.GetRatingSummariesAsync(pageIds);

            foreach (var vm in vmList)
            {
                if (ratingMap.TryGetValue(vm.DirectoryEntryId, out var rs) && rs.ReviewCount > 0)
                {
                    vm.AverageRating = rs.AvgRating;     // e.g. 4.8
                    vm.ReviewCount = rs.ReviewCount;     // e.g. 4
                }
                else
                {
                    // no approved ratings
                    vm.AverageRating = null;
                    vm.ReviewCount = null; // or 0 if you prefer non-null
                }
            }
        }

        // show sponsors first (keep rating order within each group)
        vmList = vmList.Where(e => e.IsSponsored).Concat(vmList.Where(e => !e.IsSponsored)).ToList();

        var vmOut = new SearchViewModel
        {
            Query = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            Entries = vmList
        };

        return this.View(vmOut);
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
            StringConstants.CacheKeySearchBlacklistTerms,
            norm,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });

        return norm;
    }
}
