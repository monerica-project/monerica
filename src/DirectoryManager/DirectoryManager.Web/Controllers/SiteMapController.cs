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

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache)
        {
            this.cacheService = cacheService;
            this.memoryCache = memoryCache;
        }

        [Route("sitemap_index.xml")]
        public IActionResult SiteMapIndex()
        {
            return this.RedirectPermanent("~/sitemap.xml");
        }

        [Route("sitemap.xml")]
        public IActionResult Index()
        {
            var siteMapHelper = new SiteMapHelper();
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = UrlHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = DateTime.UtcNow
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/newest", UrlHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = DateTime.UtcNow
            });

            var xml = siteMapHelper.GenerateXml();

            return this.Content(xml, "text/xml");
        }
    }
}