using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Public, read-only change history for a listing. Lives on the application
    /// (app domain) and is linked from each listing's profile page.
    /// </summary>
    [AllowAnonymous]
    [Route("history")]
    public class ListingHistoryController : BaseController
    {
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICacheService cacheHelper;
        private readonly IUrlResolutionService urlResolver;

        public ListingHistoryController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            IDirectoryEntriesAuditRepository auditRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheHelper,
            IUrlResolutionService urlResolver)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.auditRepository = auditRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheHelper = cacheHelper;
            this.urlResolver = urlResolver;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Index(int id)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (entry == null)
            {
                return this.NotFound();
            }

            var audits = await this.auditRepository.GetAuditsWithSubCategoriesForEntryAsync(id);

            var link2Label = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Label = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var listingUrl = this.urlResolver.ResolveToRoot($"/site/{entry.DirectoryEntryKey}");

            var model = ListingHistoryBuilder.Build(
                directoryEntryId: id,
                entryName: entry.Name,
                listingUrl: listingUrl,
                audits: audits,
                link2Label: link2Label,
                link3Label: link3Label);

            return this.View(model);
        }
    }
}
