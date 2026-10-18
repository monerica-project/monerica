using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
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

        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IDirectoryEntryReviewCommentRepository commentRepository;

        public HomeController(
            IDirectoryEntryRepository directoryEntryRepository,
            IRssFeedService rssFeedService,
            IMemoryCache cache,
            ICacheService cacheService,
            IDirectoryEntryReviewRepository reviewRepository,
            IDirectoryEntryReviewCommentRepository commentRepository)
            : base(cache)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.rssFeedService = rssFeedService;
            this.cache = cache;
            this.cacheService = cacheService;
            this.reviewRepository = reviewRepository;
            this.commentRepository = commentRepository;
        }

        [HttpGet("/")]
        public async Task<IActionResult> IndexAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, string.Empty);

            // ✅ Load homepage “Latest Reviews” + “Latest Comments”
            var latestReviews = await this.reviewRepository.ListLatestApprovedAsync(IntegerConstants.ReviewCountToShowOnHomepage);

            // NOTE: method name may differ in your repo — see note below
            var latestComments = await this.commentRepository.ListLatestApprovedAsync(IntegerConstants.CommentCountToShowOnHomepage);

            this.ViewBag.LatestReviews = latestReviews;
            this.ViewBag.LatestComments = latestComments;

            return this.View();
        }

        [HttpGet("contact")]
        public async Task<IActionResult> ContactAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "contact");
            return this.View();
        }

        [HttpGet("faq")]
        public async Task<IActionResult> FAQAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "faq");
            return this.View();
        }

        [HttpGet("donate")]
        public async Task<IActionResult> DonateAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "donate");
            return this.View();
        }

        [HttpGet("about")]
        public async Task<IActionResult> AboutAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "about");
            return this.View();
        }

        [HttpGet("terms")]
        public async Task<IActionResult> TermsAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "terms");
            return this.View();
        }

        [HttpGet("network")]
        public async Task<IActionResult> NetworkAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "network");
            return this.View();
        }

        [HttpGet("review-us")]
        public async Task<IActionResult> ReviewUsAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "review-us");
            return this.View();
        }

        [HttpGet("pgp")]
        public async Task<IActionResult> PgpAsync()
        {
            var pgpKey = await this.cacheService.GetSnippetAsync(SiteConfigSetting.PgpKey);
            return this.Content(pgpKey);
        }

        [HttpGet("newest")]
        [HttpGet("newest/page/{pageNumber:int}")]
        public async Task<IActionResult> Newest(int pageNumber = 1)
        {
            // Fixed page size (no longer taken from the query string) and page < 1 rejected
            // BEFORE querying; out-of-range pages 301 to page 1 like the category pages.
            const int PageSize = IntegerConstants.MaxPageSize;
            if (pageNumber < 1)
            {
                return this.RedirectPermanent("/newest");
            }

            int totalEntries = await this.directoryEntryRepository.TotalActive();
            int totalPages = PagingHelper.TotalPages(totalEntries, PageSize);
            if (pageNumber > totalPages)
            {
                return this.RedirectPermanent("/newest");
            }

            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            var basePath = "newest";
            var path = pageNumber > 1
                ? $"{basePath}/page/{pageNumber}"
                : basePath;
            this.ViewData[StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(canonicalDomain, path);

            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(PageSize, pageNumber);

            this.ViewBag.TotalEntries = totalEntries;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.PageNumber = pageNumber;

            return this.View("Newest", groupedNewestAdditions);
        }

        [Authorize]
        [HttpGet("expire-cache")]
        public IActionResult ExpireCache()
        {
            // GET only renders the confirm form. State change happens on POST below,
            // so a cross-site GET can no longer trigger a cache flush.
            return this.View();
        }

        [Authorize]
        [HttpPost("expire-cache")]
        [ValidateAntiForgeryToken]
        public IActionResult ExpireCacheConfirmed()
        {
            this.ClearCachedItems();
            this.TempData["CacheCleared"] = true;
            return this.RedirectToAction(nameof(this.ExpireCache));
        }
    }
}