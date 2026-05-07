using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

public class SearchController : Controller
{
    private static readonly Regex DonateRegex = new (
        @"\b(donate|donates|donating|donation|donations)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDirectoryEntryRepository entryRepo;
    private readonly IDirectoryEntryReviewRepository reviewRepo;
    private readonly ICacheService cacheService;
    private readonly ISearchLogRepository searchLogRepository;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly ISearchBlacklistRepository blacklistRepository;
    private readonly IMemoryCache memoryCache;
    private readonly ISearchBlacklistCache blacklistCache;

    public SearchController(
        IDirectoryEntryRepository entryRepo,
        IDirectoryEntryReviewRepository reviewRepo,
        ICacheService cacheService,
        ISearchLogRepository searchLogRepository,
        ISponsoredListingRepository sponsoredListingRepository,
        ISearchBlacklistRepository blacklistRepository,
        IMemoryCache memoryCache,
        IUrlResolutionService urlResolver,
        DirectoryManager.Web.Services.Interfaces.ISearchBlacklistCache blacklistCache)
    {
        this.entryRepo = entryRepo;
        this.reviewRepo = reviewRepo;
        this.cacheService = cacheService;
        this.searchLogRepository = searchLogRepository;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.blacklistRepository = blacklistRepository;
        this.memoryCache = memoryCache;
        this.blacklistCache = blacklistCache;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return this.BadRequest("No search performed.");
        }

        if (ScriptValidation.ContainsScriptTag(q) || HtmlValidation.ContainsHtmlTag(q))
        {
            return this.BadRequest("Invalid search.");
        }

        // 🔒 Blacklist check (case-insensitive word-boundary match)
        var qNorm = q.Trim();

        var black = await this.blacklistCache.GetTermsAsync();

        string? hit = black.FirstOrDefault(term =>
            Regex.IsMatch(
                qNorm,
                $@"\b{Regex.Escape(term)}\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

        if (hit is not null)
        {
            var blackListUrl = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SearchBlacklistRedirectUrl);
            return !string.IsNullOrWhiteSpace(blackListUrl)
                ? this.Redirect(blackListUrl)
                : this.NotFound();
        }

        // 💜 Donate pin: only on first page, when query matches donate-family terms
        bool showDonatePin = page <= 1 && DonateRegex.IsMatch(qNorm);

        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var result = await this.entryRepo.SearchNonRemovedAsync(q, page, pageSize);
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
                    vm.AverageRating = rs.AvgRating;
                    vm.ReviewCount = rs.ReviewCount;
                }
                else
                {
                    vm.AverageRating = null;
                    vm.ReviewCount = null;
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
            Entries = vmList,
            ShowDonatePin = showDonatePin
        };

        return this.View(vmOut);
    }
}