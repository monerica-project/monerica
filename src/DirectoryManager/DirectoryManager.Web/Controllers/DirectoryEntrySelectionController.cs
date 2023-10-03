using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class DirectoryEntrySelectionController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IDirectoryEntrySelectionRepository directoryEntrySelectionRepository;

        public DirectoryEntrySelectionController(
              UserManager<ApplicationUser> userManager,
              IDirectoryEntryRepository entryRepository,
              ISubCategoryRepository subCategoryRepository,
              ICategoryRepository categoryRepository,
              IDirectoryEntriesAuditRepository auditRepository,
              ITrafficLogRepository trafficLogRepository,
              UserAgentCacheService userAgentCacheService,
              IDirectoryEntrySelectionRepository directoryEntrySelectionRepository)
              : base(trafficLogRepository, userAgentCacheService)
        {
            this.userManager = userManager;
            this.entryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
            this.directoryEntrySelectionRepository = directoryEntrySelectionRepository;
        }

        public async Task<IActionResult> AddToList()
        {
            this.ViewBag.DirectoryEntryList = new SelectList(await this.entryRepository.GetAllAsync(), "Id", "Name");
            return this.View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToList(DirectoryEntrySelection selection)
        {
            if (this.ModelState.IsValid)
            {
                await this.directoryEntrySelectionRepository.AddToList(selection);
                return this.RedirectToAction("Index");
            }

            this.ViewBag.DirectoryEntryList = new SelectList(await this.entryRepository.GetAllAsync(), "Id", "Name");
            return this.View(selection);
        }

        [HttpGet]
        public IActionResult Index()
        {
            var selections = this.directoryEntrySelectionRepository.GetAll();
            return this.View(selections);
        }

        public async Task<IActionResult> DeleteFromList(int id)
        {
            var selection = await this.directoryEntrySelectionRepository.GetByID(id);
            return this.View(selection);
        }

        [HttpPost]
        [ActionName("DeleteFromListConfirmed")]
        public async Task<IActionResult> DeleteFromListConfirmed(int directoryEntrySelectionId)
        {
            await this.directoryEntrySelectionRepository.DeleteFromList(directoryEntrySelectionId);

            return this.RedirectToAction("Index");
        }
    }
}
