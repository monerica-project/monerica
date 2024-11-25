using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class RssController : Controller
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IRssFeedService rssFeedService;
        private readonly ICacheService cacheService;

        public RssController(
            IDirectoryEntryRepository directoryEntryRepository,
            IRssFeedService rssFeedService,
            ICacheService cacheService)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.rssFeedService = rssFeedService;
            this.cacheService = cacheService;
        }

        [HttpGet("rss/feed.xml")]
        public async Task<IActionResult> FeedXml()
        {
            var siteName = this.cacheService.GetSnippet(Data.Enums.SiteConfigSetting.SiteName);
            var newestEntries = await this.directoryEntryRepository.GetNewestAdditions(IntegerConstants.MaxPageSize);

            var feedLink = this.Url.Action("FeedXml", "Rss", null, this.Request.Scheme);

            if (string.IsNullOrEmpty(feedLink))
            {
                // Handle the case where feedLink is null or empty
                return this.BadRequest("Unable to generate feed link.");
            }

            var rssFeed = this.rssFeedService.GenerateRssFeed(
                newestEntries,
                $"{siteName} - {IntegerConstants.MaxPageSize} Newest Additions",
                feedLink, // Pass the validated feedLink
                "The latest additions to our directory.");

            return this.Content(rssFeed.ToString(), "application/xml");
        }
    }
}