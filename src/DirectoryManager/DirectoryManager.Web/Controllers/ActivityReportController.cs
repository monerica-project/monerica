using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Activity;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Models.Reports;
using DirectoryManager.Web.Revenue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    // Admin activity report: everything people submit to the site (new listings, listing edits,
    // reviews, review replies, problem reports, verification requests) per week and per month.
    [Authorize]
    [Route("admin/activity")]
    public class ActivityReportController : BaseController
    {
        private const string DefaultPeriod = "last-90";

        private readonly ISiteActivityRepository activityRepository;

        public ActivityReportController(ISiteActivityRepository activityRepository, IMemoryCache cache)
            : base(cache)
        {
            this.activityRepository = activityRepository;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var (key, label, start, end, items) = await this.LoadAsync(period, from, to, ct);

            return this.View(new ActivityReportViewModel
            {
                Period = key,
                PeriodLabel = label,
                From = start,
                To = end,
                PeriodOptions = RevenuePeriods.Options,
                ChartQuery = this.Request.QueryString.Value ?? string.Empty,
                Totals = ActivityReportBuilder.Totals(items, start, end),
                Weeks = Buckets(items, start, end, ActivityGranularity.Week, key),
                Months = Buckets(items, start, end, ActivityGranularity.Month, key),
            });
        }

        [HttpGet("weekly.png")]
        public Task<IActionResult> Weekly(string? period, DateTime? from, DateTime? to, CancellationToken ct = default) =>
            this.ChartAsync(ActivityGranularity.Week, period, from, to, ct);

        [HttpGet("monthly.png")]
        public Task<IActionResult> Monthly(string? period, DateTime? from, DateTime? to, CancellationToken ct = default) =>
            this.ChartAsync(ActivityGranularity.Month, period, from, to, ct);

        private static List<ActivityBucket> Buckets(
            List<SiteActivityItem> items, DateTime start, DateTime end, ActivityGranularity granularity, string periodKey)
        {
            var buckets = ActivityReportBuilder.Build(items, start, end, granularity);

            // "All time" starts at the first submission, not at the placeholder start date.
            return periodKey == "all" ? ActivityReportBuilder.TrimLeadingEmpty(buckets) : buckets;
        }

        private async Task<IActionResult> ChartAsync(
            ActivityGranularity granularity, string? period, DateTime? from, DateTime? to, CancellationToken ct)
        {
            var (key, _, start, end, items) = await this.LoadAsync(period, from, to, ct);
            var png = ActivityPlotting.Stacked(Buckets(items, start, end, granularity, key), granularity);
            this.Response.Headers.CacheControl = "private, max-age=60";
            return this.File(png, "image/png");
        }

        // Same presets and custom range as the revenue report, defaulting to the last 90 days.
        private async Task<(string Key, string Label, DateTime From, DateTime To, List<SiteActivityItem> Items)> LoadAsync(
            string? period, DateTime? from, DateTime? to, CancellationToken ct)
        {
            var range = RevenuePeriods.Resolve(string.IsNullOrWhiteSpace(period) ? DefaultPeriod : period, from, to, DateTime.UtcNow);
            var items = await this.activityRepository.GetActivityAsync(range.From, range.To, ct);
            return (range.Key, range.Label, range.From, range.To, items);
        }
    }
}
