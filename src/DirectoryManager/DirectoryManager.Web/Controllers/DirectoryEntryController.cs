using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services;
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
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;

        public DirectoryEntryController(
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository entryRepository,
            ISubcategoryRepository subCategoryRepository,
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
            await this.SetSubcategories();

            return this.View();
        }

        [Route("directoryentry/create")]
        [HttpPost]
        public async Task<IActionResult> Create(DirectoryEntry model)
        {
            if (!this.ModelState.IsValid ||
                model.DirectoryStatus == DirectoryStatus.Unknown ||
                model.SubCategoryId == 0)
            {
                await this.SetSubcategories();

                return this.View("create", model);
            }

            // Check if the link is already used
            var link = model.Link.Trim();
            var existingEntry = await this.directoryEntryRepository.GetByLinkAsync(link);
            if (existingEntry != null)
            {
                await this.SetSubcategories();

                this.ModelState.AddModelError("Link", "The provided link is already used by another entry.");
                return this.View("create", model); // Return view with model error
            }

            model.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            model.Link = link;
            model.Name = model.Name.Trim();
            model.DirectoryEntryKey = StringHelpers.UrlKey(model.Name);
            model.Description = model.Description?.Trim();
            model.Note = model.Note?.Trim();
            model.Contact = model.Contact?.Trim();
            model.Location = model.Location?.Trim();
            model.Processor = model.Processor?.Trim();
            model.LinkA = model.LinkA?.Trim();
            model.Link2 = model.Link2?.Trim();
            model.Link2A = model.Link2A?.Trim();
            model.Link3 = model.Link3?.Trim();
            model.Link3A = model.Link3A?.Trim();

            await this.directoryEntryRepository.CreateAsync(model);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
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

            // Get all subcategories without projection into an anonymous type
            var subCategories = (await this.subCategoryRepository.GetAllAsync())
                .OrderBy(sc => sc.Category.Name)
                .ThenBy(sc => sc.Name)
                .ToList();

            this.ViewBag.SubCategories = subCategories;  // Pass the actual Subcategory objects
            return this.View(entry);  // Pass the entry model for editing
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
            existingEntry.DirectoryEntryKey = StringHelpers.UrlKey(entry.Name);
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
            var audits = await this.auditRepository.GetAuditsWithSubCategoriesForEntryAsync(entryId);
            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(entryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
            {
                CreateDate = directoryEntry.CreateDate,
                UpdateDate = directoryEntry.UpdateDate,
                DateOption = Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = directoryEntry.Link,
                Name = directoryEntry.Name,
                DirectoryEntryKey = directoryEntry.DirectoryEntryKey,
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

            // Set category and subcategory names for each audit entry
            foreach (var audit in audits)
            {
                if (audit.SubCategory != null)
                {
                    audit.SubCategoryName = $"{audit.SubCategory.Category?.Name} > {audit.SubCategory.Name}";
                }
                else
                {
                    audit.SubCategoryName = "No SubCategory Assigned";
                }
            }

            return this.View(audits);
        }

        [HttpGet("directoryentry/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.directoryEntryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("directoryentry/report")]
        public IActionResult Report()
        {
            return this.View();
        }

        [HttpGet("directoryentry/weeklyplotimage")]
        public async Task<IActionResult> WeeklyPlotImageAsync()
        {
            DirectoryEntryPlotting plottingChart = new DirectoryEntryPlotting();

            var entries = await this.directoryEntryRepository.GetAllAsync();

            var imageBytes = plottingChart.CreateWeeklyPlot(entries.ToList());
            return this.File(imageBytes, "image/png");
        }

        [AllowAnonymous]
        [HttpGet("{categorykey}/{subcategorykey}/{directoryEntryKey}")]
        public async Task<IActionResult> DirectoryEntryView(string categoryKey, string subCategoryKey, string directoryEntryKey)
        {
            var canoicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);
            this.ViewData[Constants.StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canoicalDomain, $"{categoryKey}/{subCategoryKey}/{directoryEntryKey}");
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

            var existingEntry = await this.directoryEntryRepository.GetBySubCategoryAndKeyAsync(subCategory.SubCategoryId, directoryEntryKey);

            if (existingEntry == null || existingEntry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var model = new DirectoryEntryViewModel
            {
                DirectoryEntryId = existingEntry.DirectoryEntryId,
                Name = existingEntry.Name,
                DirectoryEntryKey = existingEntry.DirectoryEntryKey,
                Link = existingEntry.Link,
                LinkA = existingEntry.LinkA,
                Link2 = existingEntry.Link2,
                Link2A = existingEntry.Link2A,
                Link3 = existingEntry.Link3,
                Link3A = existingEntry.Link3A,
                DirectoryStatus = existingEntry.DirectoryStatus,
                DirectoryBadge = existingEntry.DirectoryBadge,
                Description = existingEntry.Description,
                Location = existingEntry.Location,
                Processor = existingEntry.Processor,
                Note = existingEntry.Note,
                Contact = existingEntry.Contact,
                SubCategory = existingEntry.SubCategory,
                SubCategoryId = existingEntry.SubCategoryId,
                UpdateDate = existingEntry.UpdateDate,
                CreateDate = existingEntry.CreateDate,
                Link2Name = link2Name,
                Link3Name = link3Name,
            };

            this.ViewBag.CategoryName = category.Name;
            this.ViewBag.SubCategoryName = subCategory.Name;
            this.ViewBag.CategoryKey = categoryKey;
            this.ViewBag.SubCategoryKey = subCategoryKey;

            return this.View("DirectoryEntryView", model);
        }

        private async Task SetSubcategories()
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

            subCategories.Insert(0, new { SubCategoryId = 0, DisplayName = Constants.StringConstants.SelectACategory });

            this.ViewBag.SubCategories = subCategories;
        }
    }
}