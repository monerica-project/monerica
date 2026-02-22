using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers.Management
{
    [Authorize]
    [Route("manage/tags")]
    public class TagManagementController : BaseController
    {
        private readonly ITagRepository tagRepo;
        private const int PageSize = 100;

        public TagManagementController(
            ITagRepository tagRepo,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
             : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.tagRepo = tagRepo;
        }

        // GET: /manage/tags?page=1
        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            var paged = await this.tagRepo.ListAllPagedAsync(page, PageSize);

            var vm = new TagManagementIndexViewModel
            {
                PagedTags = paged,
                CurrentPage = page,
                PageSize = PageSize
            };

            return this.View("Index", vm);
        }

        [HttpGet("create")]
        public IActionResult Create()
        {
            return this.View("Create", new TagEditViewModel());
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TagEditViewModel vm)
        {
            vm.Name = (vm.Name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                this.ModelState.AddModelError(nameof(vm.Name), "Name is required.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View("Create", vm);
            }

            var wouldBeKey = DirectoryManager.Utilities.StringExtensions.UrlKey(vm.Name);
            var existing = await this.tagRepo.GetByKeyAsync(wouldBeKey);
            if (existing != null)
            {
                this.ModelState.AddModelError(nameof(vm.Name), "A tag with that key already exists.");
                return this.View("Create", vm);
            }

            var created = await this.tagRepo.CreateAsync(vm.Name);

            this.TempData["TagMessage"] = $"Created tag: {created.Name}";

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var tag = await this.tagRepo.GetByIdAsync(id);
            if (tag is null)
            {
                return this.NotFound();
            }

            var vm = new TagEditViewModel { TagId = tag.TagId, Name = tag.Name };
            return this.View("Edit", vm);
        }

        [HttpPost("edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TagEditViewModel vm)
        {
            vm.Name = (vm.Name ?? string.Empty).Trim();

            if (id != vm.TagId)
            {
                return this.BadRequest();
            }

            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                this.ModelState.AddModelError(nameof(vm.Name), "Name is required.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View("Edit", vm);
            }

            var wouldBeKey = DirectoryManager.Utilities.StringExtensions.UrlKey(vm.Name);
            var existing = await this.tagRepo.GetByKeyAsync(wouldBeKey);
            if (existing != null && existing.TagId != id)
            {
                this.ModelState.AddModelError(nameof(vm.Name), "A tag with that key already exists.");
                return this.View("Edit", vm);
            }

            var ok = await this.tagRepo.UpdateAsync(id, vm.Name);
            if (!ok)
            {
                return this.NotFound();
            }

            this.TempData["TagMessage"] = "Tag updated.";
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var tag = await this.tagRepo.GetByIdAsync(id);
            if (tag is null)
            {
                return this.NotFound();
            }

            return this.View("Delete", tag);
        }

        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await this.tagRepo.DeleteAsync(id);

            this.TempData["TagMessage"] = "Tag deleted.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
