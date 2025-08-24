using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SearchLogController : Controller
    {
        private readonly ISearchLogRepository searchLogRepository;

        public SearchLogController(ISearchLogRepository searchLogRepository)
        {
            this.searchLogRepository = searchLogRepository;
        }

        [HttpGet("searchlog/report")]
        public async Task<IActionResult> Report([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            DateTime to = (end ?? DateTime.UtcNow).Date;
            DateTime from = (start ?? to.AddYears(-1)).Date;

            var rows = await this.searchLogRepository.GetReportAsync(from, to);

            var vm = new SearchLogReportViewModel
            {
                StartDate = from,
                EndDate = to,
                TotalTerms = rows.Sum(r => r.Count),
                ReportItems = rows
            };
            return this.View(vm);
        }

        [HttpGet("searchlog/chart")]
        public IActionResult Chart([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            DateTime to = (end ?? DateTime.UtcNow).Date;
            DateTime from = (start ?? to.AddYears(-1)).Date;

            return this.View(new SearchLogReportViewModel { StartDate = from, EndDate = to });
        }

        [HttpGet("searchlog/weekly-plot")]
        public async Task<IActionResult> WeeklySearchPlot([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            DateTime to = (end ?? DateTime.UtcNow).Date;
            DateTime from = (start ?? to.AddYears(-1)).Date;

            var weekly = await this.searchLogRepository.GetWeeklyCountsAsync(from, to);

            var plotting = new SearchAnalyticsPlotting();
            var png = plotting.CreateWeeklySearchTotalsBarChart(weekly, from, to);

            return this.File(png.Length == 0 ? Array.Empty<byte>() : png, "image/png");
        }
    }
}