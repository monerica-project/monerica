using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
    private readonly ICategoryRepository categoryRepository;
    private readonly ISubcategoryRepository subCategoryRepository;
    private readonly ITagRepository tagRepository;
    private readonly IAuthorizationService authorizationService;

    public SearchController(
        IDirectoryEntryRepository entryRepo,
        IDirectoryEntryReviewRepository reviewRepo,
        ICacheService cacheService,
        ISearchLogRepository searchLogRepository,
        ISponsoredListingRepository sponsoredListingRepository,
        ISearchBlacklistRepository blacklistRepository,
        IMemoryCache memoryCache,
        IUrlResolutionService urlResolver,
        DirectoryManager.Web.Services.Interfaces.ISearchBlacklistCache blacklistCache,
        ICategoryRepository categoryRepository,
        ISubcategoryRepository subCategoryRepository,
        ITagRepository tagRepository,
        IAuthorizationService authorizationService)
    {
        this.categoryRepository = categoryRepository;
        this.subCategoryRepository = subCategoryRepository;
        this.tagRepository = tagRepository;
        this.authorizationService = authorizationService;
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
    public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10, int debug = 0)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return this.BadRequest("No search performed.");
        }

        // Bounded input only: long terms and wildcard-only terms are never searched or logged.
        if (q.Trim().Length > 100 || q.Trim().Trim('%', '_', '\\', '*', '?', '.', ' ').Length == 0)
        {
            return this.BadRequest("Invalid search.");
        }

        // Same bounds the repository applies (SearchNonRemovedAsync clamps to 10..50), so the
        // pager math here always matches the rows actually returned.
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 10, 50);

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

        // Nothing found: try a spelling correction. Clear typos ("trocadr" → "trocador") show
        // the corrected results; a changed letter ("mixer" → "miner") is only a suggestion.
        string? correctedQuery = null;
        string? didYouMean = null;
        if (result.TotalCount == 0)
        {
            var suggestion = await this.entryRepo.SuggestSearchCorrectionAsync(q);
            if (suggestion != null)
            {
                var corrected = await this.entryRepo.SearchNonRemovedAsync(suggestion.Query, page, pageSize);
                if (corrected.TotalCount > 0)
                {
                    if (suggestion.AutoApply)
                    {
                        result = corrected;
                        correctedQuery = suggestion.Query;
                    }
                    else
                    {
                        didYouMean = suggestion.Query;
                    }
                }
            }
        }

        // Past the last page: back to page 1 of the same search (consistent with the other
        // paged pages) instead of an empty 200. Checked before logging so it isn't logged twice.
        if (page > PagingHelper.TotalPages(result.TotalCount, pageSize))
        {
            return this.Redirect(PagingRedirectHelper.FirstPageUrl("/search", this.Request.Query));
        }

        var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
        var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

        var vmList = ViewModelConverter.ConvertToViewModels(
            result.Items.ToList(),
            DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
            DirectoryManager.DisplayFormatting.Enums.ItemDisplayType.SearchResult,
            link2Name,
            link3Name);

        // Probe-shaped or machine-made terms are answered but not logged (no visitor data used).
        if (SearchLogFilter.ShouldLog(q) && SearchLogRepeatGuard.Allow(q, DateTime.UtcNow))
        {
            await this.searchLogRepository.CreateAsync(new SearchLog
            {
                Term = q.Trim()
            });
        }

        // Sponsor pinning: unchanged rules, see SearchSponsorOrdering.
        var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
        vmList = SearchSponsorOrdering.Apply(vmList, result.Items, allSponsors);

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
                    vm.VerifiedOrderCount = rs.VerifiedOrderCount;
                }
                else
                {
                    vm.AverageRating = null;
                    vm.ReviewCount = null;
                }
            }
        }

        // Links to the category/subcategory/tag/country page the search names, a site page for
        // navigational searches, and the closest categories when nothing was found.
        var effectiveQuery = correctedQuery ?? q;
        var browseTargets = await this.GetBrowseTargetsAsync();
        var browseLinks = page <= 1 ? SearchBrowseLinks.FindNamed(effectiveQuery, browseTargets) : new List<BrowseTarget>();
        var shortcut = page <= 1 ? SearchBrowseLinks.FindShortcut(q) : null;
        var closest = result.TotalCount == 0 && browseLinks.Count == 0
            ? SearchBrowseLinks.FindClosest(q, browseTargets)
            : new List<BrowseTarget>();

        // Admin-only ranking details (?debug=1).
        Dictionary<int, string>? explanations = null;
        if (debug == 1 && (await this.authorizationService.AuthorizeAsync(this.User, AuthorizationPolicies.AdminOnly)).Succeeded)
        {
            explanations = (await this.entryRepo.ExplainSearchAsync(effectiveQuery))
                .GroupBy(r => r.Entry.DirectoryEntryId)
                .ToDictionary(g => g.Key, g => g.First().Explain());
        }

        var vmOut = new SearchViewModel
        {
            Query = q,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            Entries = vmList,
            ShowDonatePin = showDonatePin,
            CorrectedQuery = correctedQuery,
            DidYouMean = didYouMean,
            BrowseLinks = browseLinks,
            Shortcut = shortcut,
            ClosestLinks = closest,
            DebugExplanations = explanations,
        };

        return this.View(vmOut);
    }

    // Every browsable page a search can point to, cached for 10 minutes.
    private async Task<List<BrowseTarget>> GetBrowseTargetsAsync()
    {
        const string cacheKey = "search:browse-targets";
        if (this.memoryCache.TryGetValue(cacheKey, out List<BrowseTarget>? cached) && cached != null)
        {
            return cached;
        }

        var targets = new List<BrowseTarget>();
        var categories = (await this.categoryRepository.GetActiveCategoriesAsync()).Where(c => c.IsEnabled).ToList();
        var enabledCategoryIds = categories.Select(c => c.CategoryId).ToHashSet();
        targets.AddRange(categories.Select(c => new BrowseTarget(c.Name, "/" + c.CategoryKey, SearchBrowseLinks.TargetKind.Category, c.Name)));

        foreach (var sc in await this.subCategoryRepository.GetAllActiveSubCategoriesAsync())
        {
            if (sc.Category != null && enabledCategoryIds.Contains(sc.CategoryId))
            {
                targets.Add(new BrowseTarget($"{sc.Category.Name} › {sc.Name}", $"/{sc.Category.CategoryKey}/{sc.SubCategoryKey}", SearchBrowseLinks.TargetKind.Subcategory, sc.Name));
            }
        }

        foreach (var tag in await this.tagRepository.ListActiveTagsAsync())
        {
            targets.Add(new BrowseTarget($"Tagged: {tag.Name}", "/tagged/" + tag.Key, SearchBrowseLinks.TargetKind.Tag, tag.Name));
        }

        var activeCountries = await this.entryRepo.ListActiveCountryCodesAsync();
        var countryNames = CountryHelper.GetCountries();
        foreach (var code in activeCountries)
        {
            if (countryNames.TryGetValue(code, out var name))
            {
                targets.Add(new BrowseTarget($"Listings in {name}", "/countries/" + StringHelpers.UrlKey(name), SearchBrowseLinks.TargetKind.Country, code));
            }
        }

        this.memoryCache.Set(cacheKey, targets, TimeSpan.FromMinutes(10));
        return targets;
    }
}
