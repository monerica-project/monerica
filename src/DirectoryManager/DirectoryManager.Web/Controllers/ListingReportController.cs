using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.ListingReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin listing reports: active listings over time, categories, countries and trust by
    /// country. Every page takes a date range (or an "as of" date) and rebuilds the directory
    /// for those dates from the listing edit history.
    /// </summary>
    [Authorize]
    [Route("directoryentry/report")]
    public class ListingReportController : BaseController
    {
        private const string HistoryCacheKey = "listing-report-history";
        private const int MaxListedChanges = 200;

        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IMemoryCache cache;

        public ListingReportController(
            IDirectoryEntriesAuditRepository auditRepository,
            IDirectoryEntryRepository entryRepository,
            ISubcategoryRepository subcategoryRepository,
            ICategoryRepository categoryRepository,
            IMemoryCache cache)
            : base(cache)
        {
            this.auditRepository = auditRepository;
            this.entryRepository = entryRepository;
            this.subcategoryRepository = subcategoryRepository;
            this.categoryRepository = categoryRepository;
            this.cache = cache;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var history = await this.GetHistoryAsync();
            var today = DateTime.UtcNow.Date;
            var now = history.ActiveOn(today);
            var (added, removed) = history.Changes(today.AddDays(-29), today);

            var vm = new ListingReportHomeViewModel
            {
                Section = string.Empty,
                Start = today.AddYears(-1),
                End = today,
                Earliest = history.Earliest,
                ActiveNow = now.Count,
                ActiveByStatus = now.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count()),
                Added30Days = added.Count,
                Removed30Days = removed.Count,
                ActiveYearAgo = history.ActiveOn(today.AddYears(-1)).Count,
            };
            return this.View(vm);
        }

        [HttpGet("active")]
        public async Task<IActionResult> Active(DateTime? start, DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (from, to) = Range(start, end, history);
            var (added, removed) = history.Changes(from, to);
            var entries = (await this.entryRepository.GetAllAsync()).ToDictionary(e => e.DirectoryEntryId);

            List<ListingReportEntryRow> Rows(IEnumerable<int> ids) => ids
                .Where(entries.ContainsKey)
                .Select(id => new ListingReportEntryRow
                {
                    DirectoryEntryId = id,
                    Name = entries[id].Name,
                    CurrentStatus = entries[id].DirectoryStatus,
                })
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxListedChanges)
                .ToList();

            return this.View(new ActiveListingsReportViewModel
            {
                Section = "active",
                Start = from,
                End = to,
                Earliest = history.Earliest,
                ActiveAtStart = history.ActiveOn(from).Count,
                ActiveAtEnd = history.ActiveOn(to).Count,
                Added = Rows(added),
                Removed = Rows(removed),
            });
        }

        [HttpGet("active-chart")]
        public async Task<IActionResult> ActiveChart(DateTime? start, DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (from, to) = Range(start, end, history);
            var step = ListingHistory.StepFor(from, to);
            var series = history.ActiveSeries(from, to, step);
            var title = $"Active listings, {Fmt(from)} to {Fmt(to)} ({step.ToString().ToLowerInvariant()}ly)";
            return this.Png(new DirectoryEntryPlotting().CreateActiveOverTimeChart(series, title));
        }

        [HttpGet("categories")]
        public async Task<IActionResult> Categories(DateTime? start, DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (from, to) = Range(start, end, history);
            var label = await this.CategoryLabelerAsync();

            var vm = Compare(history, from, to, s => label(s.SubCategoryId));
            vm.Section = "categories";
            vm.Title = "Categories";
            vm.ColumnLabel = "Category";
            return this.View("Breakdown", vm);
        }

        [HttpGet("categories-chart")]
        public async Task<IActionResult> CategoriesChart(DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (_, to) = Range(null, end, history);
            var subs = (await this.subcategoryRepository.GetAllAsync()).ToDictionary(s => s.SubCategoryId);
            var categories = await this.categoryRepository.GetAllAsync();

            var entries = history.ActiveOn(to).Select(s => new DirectoryEntry
            {
                Name = string.Empty,
                DirectoryEntryKey = string.Empty,
                DirectoryStatus = s.Status,
                SubCategory = s.SubCategoryId is int id && subs.TryGetValue(id, out var sc) ? sc : null,
            });

            return this.Png(new DirectoryEntryPlotting().CreateCategoryPieChartImage(
                entries, categories, $"Active listings by category, {Fmt(to)}"));
        }

        [HttpGet("countries")]
        public async Task<IActionResult> Countries(DateTime? start, DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (from, to) = Range(start, end, history);

            var vm = Compare(history, from, to, s => CountryLabel(s.CountryCode));
            vm.Section = "countries";
            vm.Title = "Countries";
            vm.ColumnLabel = "Country";
            return this.View("Breakdown", vm);
        }

        [HttpGet("countries-chart")]
        public async Task<IActionResult> CountriesChart(DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (_, to) = Range(null, end, history);
            return this.Png(new DirectoryEntryPlotting().CreateActiveCountriesPieChartImage(
                KnownCountryEntries(history.ActiveOn(to)), $"Active listings by country, {Fmt(to)}"));
        }

        [HttpGet("trust")]
        public async Task<IActionResult> Trust(DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (_, to) = Range(null, end, history);

            var rows = KnownCountryEntries(history.ActiveOn(to))
                .GroupBy(e => e.CountryCode!)
                .Select(g => new TrustByCountryRow
                {
                    Country = CountryHelper.GetCountryName(g.Key),
                    Verified = g.Count(e => e.DirectoryStatus == DirectoryStatus.Verified),
                    Admitted = g.Count(e => e.DirectoryStatus == DirectoryStatus.Admitted),
                    Questionable = g.Count(e => e.DirectoryStatus == DirectoryStatus.Questionable),
                    Scam = g.Count(e => e.DirectoryStatus == DirectoryStatus.Scam),
                })
                .OrderByDescending(r => r.Total)
                .ThenBy(r => r.Country)
                .ToList();

            return this.View(new TrustByCountryReportViewModel
            {
                Section = "trust",
                SingleDate = true,
                Start = to,
                End = to,
                Earliest = history.Earliest,
                Rows = rows,
            });
        }

        [HttpGet("trust-chart")]
        public async Task<IActionResult> TrustChart(DateTime? end)
        {
            var history = await this.GetHistoryAsync();
            var (_, to) = Range(null, end, history);
            return this.Png(new DirectoryEntryPlotting().CreateCountryStatusBreakdownChartImage(
                KnownCountryEntries(history.ActiveOn(to)),
                title: $"Trust by country, {Fmt(to)} (sorted by listing count)"));
        }

        /// <summary>Default range is the last 12 months; clamped to the history and to today.</summary>
        internal static (DateTime From, DateTime To) Range(DateTime? start, DateTime? end, ListingHistory history)
        {
            var today = DateTime.UtcNow.Date;
            var to = (end ?? today).Date;
            if (to > today)
            {
                to = today;
            }

            var from = (start ?? to.AddYears(-1)).Date;
            if (from > to)
            {
                (from, to) = (to, from);
            }

            if (from < history.Earliest)
            {
                from = history.Earliest;
            }

            if (to < from)
            {
                to = from;
            }

            return (from, to);
        }

        private static ListingBreakdownReportViewModel Compare(
            ListingHistory history, DateTime from, DateTime to, Func<ListingState, string?> labelOf)
        {
            var atStart = history.ActiveOn(from).Select(labelOf).ToList();
            var atEnd = history.ActiveOn(to).Select(labelOf).ToList();

            var startCounts = atStart.Where(l => l != null).GroupBy(l => l!).ToDictionary(g => g.Key, g => g.Count());
            var endCounts = atEnd.Where(l => l != null).GroupBy(l => l!).ToDictionary(g => g.Key, g => g.Count());

            return new ListingBreakdownReportViewModel
            {
                Start = from,
                End = to,
                Earliest = history.Earliest,
                MissingAtEnd = atEnd.Count(l => l == null),
                Rows = startCounts.Keys.Union(endCounts.Keys)
                    .Select(k => new ListingReportBreakdownRow
                    {
                        Label = k,
                        StartCount = startCounts.GetValueOrDefault(k),
                        EndCount = endCounts.GetValueOrDefault(k),
                    })
                    .OrderByDescending(r => r.EndCount)
                    .ThenBy(r => r.Label)
                    .ToList(),
            };
        }

        private static string? CountryLabel(string? code)
        {
            if (code is null || !CountryHelper.GetCountries().ContainsKey(code))
            {
                return null;
            }

            return CountryHelper.GetCountryName(code);
        }

        private static List<DirectoryEntry> KnownCountryEntries(IEnumerable<ListingState> states)
        {
            var known = CountryHelper.GetCountries();
            return states
                .Where(s => s.CountryCode != null && known.ContainsKey(s.CountryCode))
                .Select(s => new DirectoryEntry
                {
                    Name = string.Empty,
                    DirectoryEntryKey = string.Empty,
                    DirectoryStatus = s.Status,
                    CountryCode = s.CountryCode,
                })
                .ToList();
        }

        private static string Fmt(DateTime d) => d.ToString(DirectoryManager.Common.Constants.StringConstants.DateFormat);

        private FileContentResult Png(byte[] bytes) => this.File(bytes ?? Array.Empty<byte>(), StringConstants.PngImage);

        private async Task<Func<int?, string?>> CategoryLabelerAsync()
        {
            var subs = await this.subcategoryRepository.GetAllAsync();
            var cats = (await this.categoryRepository.GetAllAsync()).ToDictionary(c => c.CategoryId, c => c.Name);
            var catBySub = subs.ToDictionary(
                s => s.SubCategoryId,
                s => cats.TryGetValue(s.CategoryId, out var n) ? n : null);
            return id => id is int i && catBySub.TryGetValue(i, out var n) ? n : null;
        }

        /// <summary>The edit history changes only when a listing is edited; cache it briefly.</summary>
        private async Task<ListingHistory> GetHistoryAsync()
        {
            if (this.cache.TryGetValue(HistoryCacheKey, out ListingHistory? cached) && cached is not null)
            {
                return cached;
            }

            var history = new ListingHistory(await this.auditRepository.GetAllAsync());
            this.cache.Set(HistoryCacheKey, history, TimeSpan.FromMinutes(5));
            return history;
        }
    }
}
