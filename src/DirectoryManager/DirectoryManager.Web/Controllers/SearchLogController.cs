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
            var (fromUtc, toExclusiveUtc, fromDisplay, toDisplay) = NormalizeRange(start, end);

            var rows = await this.searchLogRepository.GetReportAsync(fromUtc, toExclusiveUtc);

            var vm = new SearchLogReportViewModel
            {
                StartDate = fromDisplay,
                EndDate = toDisplay,
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

        private static (DateTime fromUtc, DateTime toExclusiveUtc, DateTime fromDisplay, DateTime toDisplay)
           NormalizeRange(DateTime? start, DateTime? end)
        {
            DateTime todayUtc = DateTime.UtcNow.Date;

            // dates for display (inclusive)
            DateTime toDisplay = end?.Date ?? todayUtc;
            DateTime fromDisplay = start?.Date ?? toDisplay.AddMinutes(-1);

            if (fromDisplay > toDisplay)
            {
                (fromDisplay, toDisplay) = (toDisplay, fromDisplay);
            }

            // repo expects: CreateDate >= from && CreateDate < end
            // so make end exclusive by adding a day
            DateTime fromUtc = DateTime.SpecifyKind(fromDisplay, DateTimeKind.Utc);
            DateTime toExclusiveUtc = DateTime.SpecifyKind(toDisplay.AddDays(1), DateTimeKind.Utc);

            return (fromUtc, toExclusiveUtc, fromDisplay, toDisplay);
        }
    }
}