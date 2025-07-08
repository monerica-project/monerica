using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SearchLogController : Controller
    {
        private readonly ISearchLogRepository searchLogRepo;

        public SearchLogController(ISearchLogRepository searchLogRepo)
        {
            this.searchLogRepo = searchLogRepo;
        }

        [HttpGet("searchlog/report")]
        public async Task<IActionResult> Report(
            [FromQuery, DataType(DataType.Date)] DateTime? start,
            [FromQuery, DataType(DataType.Date)] DateTime? end)
        {
            var to = end ?? DateTime.UtcNow;
            var from = start ?? to.AddDays(-30);

            var rows = await this.searchLogRepo.GetReportAsync(from, to);

            var vm = new SearchLogReportViewModel
            {
                StartDate = from,
                EndDate = to,
                TotalTerms = rows.Sum(r => r.Count),
                ReportItems = rows
            };

            return this.View(vm);
        }
    }
}