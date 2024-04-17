using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class DirectoryEntryController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;

        public DirectoryEntryController(
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository entryRepository,
            ISubCategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntriesAuditRepository auditRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ICacheService cacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.entryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
            this.cache = cache;
            this.cacheService = cacheService;
       }

        public async Task<IActionResult> Index(int? subCategoryId = null)
        {
            var entries = await this.entryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory != null && e.SubCategory.SubCategoryId == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllAsync())
                                    .OrderBy(sc => sc.Category.Name)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return this.View(entries);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var subCategories = (await this.subCategoryRepository.GetAllAsync())
                .OrderBy(sc => sc.Category.Name)
                .ThenBy(sc => sc.Name)
                .Select(sc => new
                {
                    sc.SubCategoryId,
                    DisplayName = $"{sc.Category.Name} > {sc.Name}"
                })
                .ToList();

            subCategories.Insert(0, new { SubCategoryId = 0, DisplayName = "Please select a category" });

            this.ViewBag.SubCategories = subCategories;

            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(DirectoryEntry entry)
        {
            if (this.ModelState.IsValid)
            {
                entry.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
                entry.SubCategoryId = entry.SubCategoryId;
                entry.Link = entry.Link.Trim();
                entry.Link2 = entry.Link2?.Trim();
                entry.Link3 = entry.Link3?.Trim();
                entry.Name = entry.Name.Trim();
                entry.Description = entry.Description?.Trim();
                entry.Note = entry.Note?.Trim();
                entry.DirectoryStatus = entry.DirectoryStatus;
                entry.Contact = entry.Contact?.Trim();
                entry.Location = entry.Location?.Trim();
                entry.Processor = entry.Processor?.Trim();

                await this.entryRepository.CreateAsync(entry);

                this.ClearCachedItems();

                return this.RedirectToAction(nameof(this.Index));
            }
            else
            {
                return this.View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await this.entryRepository.GetByIdAsync(id);
            if (entry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SubCategories = await this.subCategoryRepository.GetAllAsync();
            return this.View(entry);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(DirectoryEntry entry)
        {
            var existingEntry = await this.entryRepository.GetByIdAsync(entry.DirectoryEntryId);

            if (existingEntry == null)
            {
                return this.NotFound();
            }

            existingEntry.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.Link = entry.Link.Trim();
            existingEntry.Link2 = entry.Link2?.Trim();
            existingEntry.Link3 = entry.Link3?.Trim();
            existingEntry.Name = entry.Name.Trim();
            existingEntry.Description = entry.Description?.Trim();
            existingEntry.Note = entry.Note?.Trim();
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Contact = entry.Contact?.Trim();
            existingEntry.Location = entry.Location?.Trim();
            existingEntry.Processor = entry.Processor?.Trim();

            await this.entryRepository.UpdateAsync(existingEntry);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("directoryentries/EntryAudits/{entryId}")]
        public async Task<IActionResult> EntryAudits(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsForEntryAsync(entryId);
            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var directoryEntry = await this.entryRepository.GetByIdAsync(entryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
            {
                DirectoryEntry = directoryEntry,
                Link2Name = link2Name,
                Link3Name = link3Name
            };

            return this.View(audits);
        }

        public async Task<IActionResult> Delete(int id)
        {
            await this.entryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}