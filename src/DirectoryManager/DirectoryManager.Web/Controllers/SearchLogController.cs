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
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end)
        {
            // normalize "to" = end of requested window (or now)
            DateTime to = end.HasValue
                ? end.Value
                : DateTime.UtcNow;

            // normalize "from" = start of window (or last 24h)
            DateTime from = start.HasValue
                ? start.Value
                : to.AddHours(-24);

            // fetch
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