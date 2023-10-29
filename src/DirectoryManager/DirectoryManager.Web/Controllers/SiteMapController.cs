using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class SiteMapController : Controller
    {
        private const int MaxPageSizeForSiteMap = 50000;

        private readonly ICacheService cacheService;
        private readonly IMemoryCache memoryCache;
        private readonly IDirectoryEntryRepository directoryEntryRepository;

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            this.cacheService = cacheService;
            this.memoryCache = memoryCache;
            this.directoryEntryRepository = directoryEntryRepository;
        }

        [Route("sitemap_index.xml")]
        public IActionResult SiteMapIndex()
        {
            return this.RedirectPermanent("~/sitemap.xml");
        }

        [Route("sitemap.xml")]
        public IActionResult Index()
        {
            var date = this.directoryEntryRepository.GetLastRevisionDate();
            var siteMapHelper = new SiteMapHelper();
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = WebRequestHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/newest", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
            });

            var xml = siteMapHelper.GenerateXml();

            return this.Content(xml, "text/xml");
        }
    }
}