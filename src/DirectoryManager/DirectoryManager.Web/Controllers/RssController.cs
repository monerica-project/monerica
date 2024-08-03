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

            var rssFeed = this.rssFeedService.GenerateRssFeed(
                newestEntries,
                "Newest Additions",
                this.Url.Action("FeedXml", "Rss", null, this.Request.Scheme), // Generate full URL for feed link
                "The latest additions to our directory.");

            return this.Content(rssFeed.ToString(), "application/xml");
        }
    }
}
