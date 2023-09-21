using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

        public DirectoryEntryController(
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository entryRepository,
            ISubCategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntriesAuditRepository auditRepository,
            ITrafficLogRepository trafficLogRepository,
            UserAgentCacheService userAgentCacheService)
            : base(trafficLogRepository, userAgentCacheService)
        {
            this.userManager = userManager;
            this.entryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
        }

        public async Task<IActionResult> Index(int? subCategoryId = null)
        {
            var entries = await this.entryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory != null && e.SubCategory.Id == subCategoryId.Value).ToList();
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
                    sc.Id,
                    DisplayName = $"{sc.Category.Name} > {sc.Name}"
                })
                .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

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
                entry.Name = entry.Name.Trim();
                entry.Description = entry.Description?.Trim();
                entry.Note = entry.Note?.Trim();
                entry.DirectoryStatus = entry.DirectoryStatus;
                entry.Contact = entry.Contact?.Trim();
                entry.Location = entry.Location?.Trim();
                entry.Processor = entry.Processor?.Trim();

                await this.entryRepository.CreateAsync(entry);
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
            var existingEntry = await this.entryRepository.GetByIdAsync(entry.Id);

            if (existingEntry == null)
            {
                return this.NotFound();
            }

            entry.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.Link = entry.Link.Trim();
            existingEntry.Link2 = entry.Link2?.Trim();
            existingEntry.Name = entry.Name.Trim();
            existingEntry.Description = entry.Description?.Trim();
            existingEntry.Note = entry.Note?.Trim();
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Contact = entry.Contact?.Trim();
            existingEntry.Location = entry.Location?.Trim();

            await this.entryRepository.UpdateAsync(existingEntry);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("directoryentries/EntryAudits/{entryId}")]
        public async Task<IActionResult> EntryAudits(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsForEntryAsync(entryId);
            return this.View(audits);
        }

        public async Task<IActionResult> Delete(int id)
        {
            await this.entryRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}