using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// "How listings are verified" (/how-we-verify): a plain explainer of the checks the directory
    /// really runs. The text is a Razor view (not a DB snippet) because it quotes rules from code
    /// (sponsor minimum age, the "New" label period) and the site name from config.
    /// </summary>
    [AllowAnonymous]
    public class HowWeVerifyController : BaseController
    {
        public const string PagePath = "how-we-verify";

        private readonly ICacheService cacheService;

        public HowWeVerifyController(IMemoryCache cache, ICacheService cacheService)
            : base(cache)
        {
            this.cacheService = cacheService;
        }

        [HttpGet("how-we-verify")]
        public async Task<IActionResult> Index()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.PageMeta().CanonicalUrl = UrlBuilder.CombineUrl(canonicalDomain, PagePath);
            return this.View("Index");
        }
    }
}
