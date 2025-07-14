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
            DateTime to = end.HasValue
                ? end.Value.Date.AddDays(1).AddTicks(-1) 
                : DateTime.UtcNow;

            DateTime from = start.HasValue
                ? start.Value.Date
                : to.Date.AddDays(-30);

            // 3) Fetch and render
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