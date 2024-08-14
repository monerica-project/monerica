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

        public RssController(IDirectoryEntryRepository directoryEntryRepository, IRssFeedService rssFeedService)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.rssFeedService = rssFeedService;
        }

        [HttpGet("rss/feed.xml")]
        public async Task<IActionResult> FeedXml()
        {
            var newestEntries = await this.directoryEntryRepository.GetNewestAdditions(IntegerConstants.MaxPageSize);

            var feedLink = this.Url.Action("FeedXml", "Rss", null, this.Request.Scheme);

            if (string.IsNullOrEmpty(feedLink))
            {
                // Handle the case where feedLink is null or empty
                return this.BadRequest("Unable to generate feed link.");
            }

            var rssFeed = this.rssFeedService.GenerateRssFeed(
                newestEntries,
                "Newest Additions",
                feedLink, // Pass the validated feedLink
                "The latest additions to our directory.");

            return this.Content(rssFeed.ToString(), "application/xml");
        }
    }
}
