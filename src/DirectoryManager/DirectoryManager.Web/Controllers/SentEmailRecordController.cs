using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SentEmailRecordController : Controller
    {
        private readonly ISentEmailRecordRepository sentEmailRecordRepository;

        public SentEmailRecordController(ISentEmailRecordRepository sentEmailRecordRepository)
        {
            this.sentEmailRecordRepository = sentEmailRecordRepository;
        }

        [Route("email/sentrecords")] // Base route for the controller
        public IActionResult Index(int page = 1, int pageSize = 10)
        {
            const int MinPage = 1;
            page = Math.Max(page, MinPage); // Ensure page is not below 1

            var pageIndex = page - 1; // For zero-based pagination
            var records = this.sentEmailRecordRepository.GetPagedRecords(pageIndex, pageSize, out int totalRecords);

            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.TotalPages = totalPages;

            return this.View(records);
        }
    }
}