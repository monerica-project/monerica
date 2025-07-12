using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Route("tagged")]
    public class TagController : Controller
    {
        private const int PageSize = 10;
        private readonly ITagRepository tagRepo;
        private readonly IDirectoryEntryTagRepository entryTagRepo;
        private readonly ICacheService cacheService;

        public TagController(
            ITagRepository tagRepo,
            IDirectoryEntryTagRepository entryTagRepo,
            ICacheService cacheService)
        {
            this.tagRepo = tagRepo ?? throw new ArgumentNullException(nameof(tagRepo));
            this.entryTagRepo = entryTagRepo ?? throw new ArgumentNullException(nameof(entryTagRepo));
            this.cacheService = cacheService;
        }

        [HttpGet("{tagSlug}")]
        public async Task<IActionResult> Index(string tagSlug, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(tagSlug))
            {
                return this.NotFound();
            }

            var tag = await this.tagRepo.GetBySlugAsync(tagSlug);
            if (tag == null)
            {
                return this.NotFound();
            }

            // get the paged raw entries
            var paged = await this.entryTagRepo.ListEntriesForTagPagedAsync(
                tag.Name,
                page,
                PageSize);

            // grab link-2/3 names just once
            var link2 = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3 = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            // convert all entries in one call
            IReadOnlyList<DirectoryEntryViewModel> vms =
                ViewModelConverter.ConvertToViewModels(
                    paged.Items.ToList(),
                    DateDisplayOption.NotDisplayed,
                    ItemDisplayType.Normal,
                    link2,
                    link3);

            var vm = new TaggedEntriesViewModel
            {
                Tag = tag,
                PagedEntries = new PagedResult<DirectoryEntryViewModel>
                {
                    Items = vms,
                    TotalCount = paged.TotalCount
                },
                CurrentPage = page,
                PageSize = PageSize
            };

            return this.View(vm);
        }
    }
}