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
        private readonly IDirectoryEntryRepository directoryEntryRepository;
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
            this.directoryEntryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
            this.cache = cache;
            this.cacheService = cacheService;
        }

        [Route("directoryentry/index")]
        public async Task<IActionResult> Index(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();

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

        [Route("directoryentry/create")]
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

        [Route("directoryentry/create")]
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

                await this.directoryEntryRepository.CreateAsync(entry);

                this.ClearCachedItems();

                return this.RedirectToAction(nameof(this.Index));
            }
            else
            {
                return this.View("Error");
            }
        }

        [Route("directoryentry/edit/{id}")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (entry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SubCategories = await this.subCategoryRepository.GetAllAsync();
            return this.View(entry);
        }

        [Route("directoryentry/edit/{id}")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, DirectoryEntry entry)
        {
            var existingEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (existingEntry == null)
            {
                return this.NotFound();
            }

            existingEntry.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.Link = entry.Link.Trim();
            existingEntry.LinkA = entry.LinkA?.Trim();
            existingEntry.Link2 = entry.Link2?.Trim();
            existingEntry.Link2A = entry.Link2A?.Trim();
            existingEntry.Link3 = entry.Link3?.Trim();
            existingEntry.Link3A = entry.Link3A?.Trim();
            existingEntry.Name = entry.Name.Trim();
            existingEntry.Description = entry.Description?.Trim();
            existingEntry.Note = entry.Note?.Trim();
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Contact = entry.Contact?.Trim();
            existingEntry.Location = entry.Location?.Trim();
            existingEntry.Processor = entry.Processor?.Trim();

            await this.directoryEntryRepository.UpdateAsync(existingEntry);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        [Route("directoryentry/entryaudits/{entryId}")]
        public async Task<IActionResult> EntryAudits(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsForEntryAsync(entryId);
            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(entryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
            {
                DateOption = Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = directoryEntry.Link,
                Name = directoryEntry.Name,
                Contact = directoryEntry.Contact,
                Description = directoryEntry.Description,
                DirectoryEntryId = directoryEntry.DirectoryEntryId,
                DirectoryStatus = directoryEntry.DirectoryStatus,
                Link2 = directoryEntry.Link2,
                Link3 = directoryEntry.Link3,
                Location = directoryEntry.Location,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
            };

            return this.View(audits);
        }

        [HttpGet("directoryentry/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.directoryEntryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [AllowAnonymous]
        [HttpGet("{categorykey}/{subcategorykey}/{listingkey}")]
        public async Task<IActionResult> SubCategoryListings(string categoryKey, string subCategoryKey, string listingkey)
        {
            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);

            if (category == null)
            {
                return this.NotFound();
            }

            var subCategory = await this.subCategoryRepository.GetByCategoryIdAndKeyAsync(category.CategoryId, subCategoryKey);

            if (subCategory == null)
            {
                return this.NotFound();
            }

            var entries = await this.directoryEntryRepository.GetActiveEntriesByCategoryAsync(subCategory.SubCategoryId);

            var model = new CategorySubCategoriesViewModel
            {
                PageHeader = $"{category.Name} > {subCategory.Name}",
                PageTitle = $"{category.Name} > {subCategory.Name}",
                MetaDescription = subCategory.MetaDescription,
                PageDetails = subCategory.PageDetails,
                Description = subCategory.Description,
                Note = subCategory.Note,
                SubCategoryId = subCategory.SubCategoryId,
                DirectoryEntries = entries,
                CategoryRelativePath = string.Format("/{0}", category.CategoryKey),
                CategoryName = category.Name
            };

            return this.View("SubCategoryListings", model);
        }
    }
}