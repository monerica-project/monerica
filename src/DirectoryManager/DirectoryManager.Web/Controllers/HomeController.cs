using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISubmissionRepository submissionRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;

        public HomeController(
            ISubmissionRepository submissionRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            this.submissionRepository = submissionRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
        }

        public IActionResult Index()
        {
            return this.View();
        }

        [HttpGet("newest")]
        public async Task<IActionResult> Newest(int pageNumber = 1, int pageSize = 25)
        {
            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(pageSize, pageNumber);

            // To determine the total number of pages, count all entries in the DB and divide by pageSize
            int totalEntries = await this.directoryEntryRepository.TotalActive();
            this.ViewBag.TotalEntries = totalEntries;
            this.ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            this.ViewBag.PageNumber = pageNumber;

            return this.View(groupedNewestAdditions);
        }
    }
}