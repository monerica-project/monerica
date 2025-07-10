using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Route("tagged")]
    public class TagController : Controller
    {
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
        public async Task<IActionResult> Index(string tagSlug)
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

            var entries = await this.entryTagRepo.ListEntriesForTagAsync(tag.Name);
            var active = entries
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed)
                .OrderBy(e => e.Name)
                .ToList();

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var vm = new TaggedEntriesViewModel
            {
                Tag = tag,
                Entries = ViewModelConverter.ConvertToViewModels(
                    active,
                    DateDisplayOption.NotDisplayed,
                    ItemDisplayType.Normal,
                    link2Name,
                    link3Name)
            };

            return this.View(vm);
        }

    }
}
