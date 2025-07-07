using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SearchController : Controller
    {
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ICacheService cacheService;
        private readonly ISearchLogRepository searchLogRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        public SearchController(
            IDirectoryEntryRepository entryRepo,
            ICacheService cacheService,
            ISearchLogRepository searchLogRepository,
            ISponsoredListingRepository sponsoredListingRepository)
        {
            this.entryRepo = entryRepo;
            this.cacheService = cacheService;
            this.searchLogRepository = searchLogRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return this.BadRequest("No search performed.");
            }

            // 1) run the repository search
            var result = await this.entryRepo.SearchAsync(q ?? "", page, pageSize);

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            // 2) map to your view‐models
            var vmList = ViewModelConverter.ConvertToViewModels(
                result.Items.ToList(),
                DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                DisplayFormatting.Enums.ItemDisplayType.SearchResult,
                link2Name,
                link3Name);

            await this.searchLogRepository.CreateAsync(new SearchLog
            {
                Term = q,
                IpAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            var allSponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();

            foreach (var item in vmList)
            {
                item.IsSponsored = allSponsors.Any(x => x.DirectoryEntryId == item.DirectoryEntryId);
            }

            // 🍾 Bubble all the sponsored entries to the top, preserving their relative order
            vmList = vmList
                .Where(e => e.IsSponsored)
                .Concat(vmList.Where(e => !e.IsSponsored))
                .ToList();

            // 3) build pager + query
            var vm = new SearchViewModel
            {
                Query = q ?? "",
                Page = page,
                PageSize = pageSize,
                TotalCount = result.TotalCount,
                Entries = vmList
            };

            return this.View(vm);
        }
    }
}