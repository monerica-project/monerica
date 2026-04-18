using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class DirectoryEntrySelectionController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IDirectoryEntrySelectionRepository directoryEntrySelectionRepository;
        private readonly IMemoryCache cache;

        public DirectoryEntrySelectionController(
              UserManager<ApplicationUser> userManager,
              IDirectoryEntryRepository entryRepository,
              ISubcategoryRepository subCategoryRepository,
              ICategoryRepository categoryRepository,
              IDirectoryEntriesAuditRepository auditRepository,
              ITrafficLogRepository trafficLogRepository,
              IUserAgentCacheService userAgentCacheService,
              IDirectoryEntrySelectionRepository directoryEntrySelectionRepository,
              IMemoryCache cache)
              : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.entryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
            this.directoryEntrySelectionRepository = directoryEntrySelectionRepository;
            this.cache = cache;
        }

        [Route("directoryentryselection/addtolist")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToList(DirectoryEntrySelection selection)
        {
            if (this.ModelState.IsValid)
            {
                await this.directoryEntrySelectionRepository.AddToList(selection);

                this.cache.Remove(StringConstants.CacheKeyEntries);

                return this.RedirectToAction("Index");
            }

            this.ViewBag.DirectoryEntryList = new SelectList(
                await this.entryRepository.GetAllActiveEntries(), "DirectoryEntryId", "Name");
            return this.View(selection);
        }

        [Route("directoryentryselection/index")]
        [HttpGet]
        public IActionResult Index()
        {
            var selections = this.directoryEntrySelectionRepository.GetAll();
            return this.View(selections);
        }

        [Route("directoryentryselection/deletefromlist")]
        public async Task<IActionResult> DeleteFromList(int id)
        {
            var selection = await this.directoryEntrySelectionRepository.GetByID(id);

            this.ViewBag.EntryDeletedFromList = await this.entryRepository.GetByIdAsync(selection.DirectoryEntryId);

            this.cache.Remove(StringConstants.CacheKeyEntries);

            return this.View(selection);
        }

        [HttpPost]
        [Route("directoryentryselection/deletefromlistconfirmed")]
        public async Task<IActionResult> DeleteFromListConfirmed(int directoryEntrySelectionId)
        {
            await this.directoryEntrySelectionRepository.DeleteFromList(directoryEntrySelectionId);

            return this.RedirectToAction("Index");
        }

        [Route("directoryentryselection/addtolist")]
        [HttpGet]
        public async Task<IActionResult> AddToList()
        {
            this.ViewBag.DirectoryEntryList = new SelectList(
                await this.entryRepository.GetAllActiveEntries(), "DirectoryEntryId", "Name");
            return this.View();
        }
    }
}