using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ISubmissionRepository submissionRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IMemoryCache cache;

        public HomeController(
            ISubmissionRepository submissionRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.submissionRepository = submissionRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cache = cache;
        }

        [HttpGet("/")]
        public IActionResult Index()
        {
            return this.View();
        }

        [HttpGet("expire-cache")]
        public IActionResult ExpireCache()
        {
            this.cache.Remove(StringConstants.EntriesCacheKey);
            this.cache.Remove(StringConstants.SponsoredListingsCacheKey);

            return this.View();
        }

        [HttpGet("newest")]
        public async Task<IActionResult> Newest(int pageNumber = 1, int pageSize = IntegerConstants.DefaultPageSize)
        {
            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(pageSize, pageNumber);

            // To determine the total number of pages, count all entries in the DB and divide by pageSize
            int totalEntries = await this.directoryEntryRepository.TotalActive();
            this.ViewBag.TotalEntries = totalEntries;
            this.ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            this.ViewBag.PageNumber = pageNumber;

            return this.View(groupedNewestAdditions);
        }
    }
}