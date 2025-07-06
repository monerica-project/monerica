using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
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

        // GET /tagged/{tagSlug}
        [HttpGet("{tagSlug}")]
        public async Task<IActionResult> Index(string tagSlug)
        {
            if (string.IsNullOrWhiteSpace(tagSlug))
            {
                return this.NotFound();
            }

            tagSlug = tagSlug.ToLowerInvariant();

            // fetch all tags, find the one matching slug:
            var allTags = await this.tagRepo.ListAllAsync();
            var tag = allTags.FirstOrDefault(t =>
                // allow "web-hosting" → "web hosting" or exact hyphenated
                string.Equals(t.Name.Replace(" ", "-"), tagSlug, StringComparison.OrdinalIgnoreCase)
             || string.Equals(t.Name, tagSlug.Replace("-", " "), StringComparison.OrdinalIgnoreCase));

            if (tag == null)
            {
                return this.NotFound();
            }

            // load all entries for that tag
            var entries = (await this.entryTagRepo.ListEntriesForTagAsync(tag.Name))
                          .OrderBy(e => e.Name)
                          .ToList();
            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var viewModelList = DirectoryManager.DisplayFormatting.Helpers.ViewModelConverter.ConvertToViewModels(
                  entries.Where(x => x.DirectoryStatus != DirectoryStatus.Removed).ToList(),
                  DirectoryManager.DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                  DirectoryManager.DisplayFormatting.Enums.ItemDisplayType.Normal,
                  link2Name,
                  link3Name);

            var vm = new TaggedEntriesViewModel
            {
                Tag = tag,
                Entries = viewModelList
            };

            return this.View(vm);
        }
    }
}
