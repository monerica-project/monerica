using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IDirectoryEntryRepository _directoryEntryRepository;

        public HomeController(
            ISubmissionRepository submissionRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            _submissionRepository = submissionRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _directoryEntryRepository = directoryEntryRepository;
        }

        [ResponseCache(Duration = 60)] // Cache for 60 seconds
        public async Task<IActionResult> IndexAsync()
        {
            return View();
        }

        [HttpGet("newest")]
        public async Task<IActionResult> Newest(int pageNumber = 1, int pageSize = 25)
        {
            var groupedNewestAdditions = await _directoryEntryRepository.GetNewestAdditionsGrouped(pageSize, pageNumber);

            // To determine the total number of pages, count all entries in the DB and divide by pageSize
            int totalEntries = await _directoryEntryRepository.TotalActive();
            ViewBag.TotalEntries = totalEntries;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            ViewBag.PageNumber = pageNumber;

            return View(groupedNewestAdditions);
        }
    }
}