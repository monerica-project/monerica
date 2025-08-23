using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IRssFeedService rssFeedService;
        private readonly IMemoryCache cache;
        private readonly ICacheService cacheService;

        public HomeController(
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IRssFeedService rssFeedService,
            IMemoryCache cache,
            ICacheService cacheService)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.rssFeedService = rssFeedService;
            this.cache = cache;
            this.cacheService = cacheService;
        }

        [HttpGet("/")]
        public IActionResult Index()
        {
            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "");
            return this.View();
        }

        [HttpGet("contact")]
        public IActionResult Contact()
        {
            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "contact");
            return this.View();
        }

        [HttpGet("faq")]
        public IActionResult FAQ()
        {
            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "faq");
            return this.View();
        }

        [HttpGet("donate")]
        public IActionResult Donate()
        {
            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "donate");
            return this.View();
        }

        [HttpGet("newest")]
        [HttpGet("newest/page/{pageNumber:int}")]
        public async Task<IActionResult> Newest(int pageNumber = 1, int pageSize = IntegerConstants.MaxPageSize)
        {
            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            var basePath = "newest";
            var path = pageNumber > 1
                ? $"{basePath}/page/{pageNumber}"
                : basePath;
            this.ViewData[StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(canonicalDomain, path);

            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(pageSize, pageNumber);

            int totalEntries = await this.directoryEntryRepository.TotalActive();
            this.ViewBag.TotalEntries = totalEntries;
            this.ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            this.ViewBag.PageNumber = pageNumber;

            return this.View("Newest", groupedNewestAdditions);
        }

        [Authorize]
        [HttpGet("expire-cache")]
        public IActionResult ExpireCache()
        {
            this.cache.Remove(StringConstants.CacheKeyEntries);
            this.cache.Remove(StringConstants.CacheKeySponsoredListings);

            return this.View();
        }
    }
}