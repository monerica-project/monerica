using System.Globalization;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class FilterLogController : Controller
    {
        private readonly IDirectoryFilterLogRepository filterLogRepo;
        private readonly ICategoryRepository categoryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly ITagRepository tagRepo;

        public FilterLogController(
            IDirectoryFilterLogRepository filterLogRepo,
            ICategoryRepository categoryRepo,
            ISubcategoryRepository subcategoryRepo,
            ITagRepository tagRepo)
        {
            this.filterLogRepo = filterLogRepo;
            this.categoryRepo = categoryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.tagRepo = tagRepo;
        }

        [HttpGet("filterlog/report")]
        public async Task<IActionResult> Report([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            var (fromUtc, toExclusiveUtc, fromDisplay, toDisplay) = NormalizeRange(start, end);

            var logs = await this.filterLogRepo.GetInRangeAsync(fromUtc, toExclusiveUtc);
            int total = logs.Count;

            // Decode maps (IDs → human names)
            var catNames = (await this.categoryRepo.GetAllAsync()).ToDictionary(c => c.CategoryId, c => c.Name);

            // Subcategory labels are prefixed with their PARENT CATEGORY ("Category › Subcategory").
            // This both disambiguates subcategories that share the same name across different
            // categories AND keeps them counted separately in the ranking.
            var subs = await this.subcategoryRepo.GetAllAsync();
            var subLabels = subs.ToDictionary(
                s => s.SubCategoryId,
                s => $"{(catNames.TryGetValue(s.CategoryId, out var cn) ? cn : "#" + s.CategoryId)} › {s.Name}");

            var tagNames = (await this.tagRepo.ListAllAsync()).ToDictionary(t => t.TagId, t => t.Name);

            List<FilterReportItem> Rank(IEnumerable<string?> labels) =>
                labels.Where(l => !string.IsNullOrWhiteSpace(l))
                      .GroupBy(l => l!)
                      .Select(g => new FilterReportItem
                      {
                          Label = g.Key,
                          Count = g.Count(),
                          Percentage = total == 0 ? 0 : g.Count() * 100.0 / total,
                      })
                      .OrderByDescending(x => x.Count)
                      .ThenBy(x => x.Label)
                      .ToList();

            var categories = Rank(logs.Where(l => l.CategoryId.HasValue)
                .Select(l => catNames.TryGetValue(l.CategoryId!.Value, out var n) ? n : $"#{l.CategoryId}"));

            var subcategories = Rank(logs.Where(l => l.SubCategoryId.HasValue)
                .Select(l => subLabels.TryGetValue(l.SubCategoryId!.Value, out var n) ? n : $"#{l.SubCategoryId}"));

            // Statuses are stored as a comma-joined set per log row; rank each status individually.
            var statuses = Rank(logs.Where(l => !string.IsNullOrWhiteSpace(l.Statuses))
                .SelectMany(l => l.Statuses!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

            var terms = Rank(logs.Select(l => l.SearchTerm));

            var countries = Rank(logs.Where(l => !string.IsNullOrWhiteSpace(l.CountryCode))
                .Select(l => CountryName(l.CountryCode!)));

            var tags = Rank(logs.Where(l => !string.IsNullOrWhiteSpace(l.TagIds))
                .SelectMany(l => l.TagIds!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => int.TryParse(s, out var id)
                    ? (tagNames.TryGetValue(id, out var n) ? n : $"#{id}")
                    : null));

            var vm = new FilterLogReportViewModel
            {
                StartDate = fromDisplay,
                EndDate = toDisplay,
                TotalEvents = total,
                Categories = categories,
                Subcategories = subcategories,
                Statuses = statuses,
                Tags = tags,
                Terms = terms,
                Countries = countries,
                VideoCount = logs.Count(l => l.HasVideo),
                TorCount = logs.Count(l => l.HasTor),
                I2pCount = logs.Count(l => l.HasI2p),
                LeadingCategory = categories.FirstOrDefault()?.Label,
                LeadingSubcategory = subcategories.FirstOrDefault()?.Label,
            };

            return this.View(vm);
        }

        private static string CountryName(string code)
        {
            try { return new RegionInfo(code).EnglishName; }
            catch { return code; }
        }

        private static (DateTime fromUtc, DateTime toExclusiveUtc, DateTime fromDisplay, DateTime toDisplay)
            NormalizeRange(DateTime? start, DateTime? end)
        {
            DateTime todayUtc = DateTime.UtcNow.Date;

            DateTime toDisplay = end?.Date ?? todayUtc;
            DateTime fromDisplay = start?.Date ?? toDisplay.AddDays(-7);

            if (fromDisplay > toDisplay)
            {
                (fromDisplay, toDisplay) = (toDisplay, fromDisplay);
            }

            DateTime fromUtc = DateTime.SpecifyKind(fromDisplay, DateTimeKind.Utc);
            DateTime toExclusiveUtc = DateTime.SpecifyKind(toDisplay.AddDays(1), DateTimeKind.Utc);

            return (fromUtc, toExclusiveUtc, fromDisplay, toDisplay);
        }
    }
}
