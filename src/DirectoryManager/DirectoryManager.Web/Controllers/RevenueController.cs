using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Models.Reports;
using DirectoryManager.Web.Revenue;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    // Admin revenue section: every income source by period (pie + monthly chart) and the
    // revenue forecast.
    [Authorize]
    [Route("admin/revenue")]
    public class RevenueController : BaseController
    {
        private const string ForecastCacheKeyPrefix = "admin-revenue-forecast:";
        private static readonly TimeSpan ForecastCacheFor = TimeSpan.FromMinutes(10);

        private readonly ISponsoredListingInvoiceRepository invoiceRepository;
        private readonly IRevenueRepository revenueRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly IMemoryCache cache;

        public RevenueController(
            ISponsoredListingInvoiceRepository invoiceRepository,
            IRevenueRepository revenueRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository,
            IMemoryCache cache)
            : base(cache)
        {
            this.invoiceRepository = invoiceRepository;
            this.revenueRepository = revenueRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
            this.cache = cache;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to, RevenueBasis basis = RevenueBasis.Earned, string? currency = null, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var range = RevenuePeriods.Resolve(period, from, to, now);
            var inXmr = IsXmr(currency);
            var (invoices, oneTime) = await this.LoadAsync(ct);
            var (xmrInvoices, xmrOneTime) = ToXmr(invoices, oneTime);
            var (notXmrCount, notXmrUsd) = XmrRevenueConverter.NotPaidInXmr(invoices, oneTime, range.From, range.To);

            var usdReport = RevenueReportBuilder.Build(invoices, oneTime, range.From, range.To, basis, now);
            var xmrReport = RevenueReportBuilder.Build(xmrInvoices, xmrOneTime, range.From, range.To, basis, now, decimals: 8);
            var report = inXmr ? xmrReport : usdReport;

            if (range.Key == "all")
            {
                // "All time" starts at the first payment, not at the placeholder start date.
                var trimmed = RevenueReportBuilder.TrimLeadingEmptyMonths(report.Months);
                report.Months.Clear();
                report.Months.AddRange(trimmed);
            }

            return this.View(new RevenueReportViewModel
            {
                Period = range.Key,
                PeriodLabel = range.Label,
                From = range.From,
                To = range.To,
                Basis = basis,
                Report = report,
                PeriodOptions = RevenuePeriods.Options,
                ChartQuery = this.Request.QueryString.Value ?? string.Empty,
                MissingUsdCount = report.Totals.Sum(t => t.MissingUsdCount),
                InXmr = inXmr,
                OtherUnitTotal = inXmr ? usdReport.GrandTotal : xmrReport.GrandTotal,
                NotPaidInXmrCount = notXmrCount,
                NotPaidInXmrUsd = notXmrUsd,
                ChartMonths = inXmr
                    ? ChartMonths(xmrInvoices, xmrOneTime, range, basis, now)
                    : ChartMonths(invoices, oneTime, range, basis, now),
            });
        }

        [HttpGet("pie.png")]
        public async Task<IActionResult> Pie(string? period, DateTime? from, DateTime? to, RevenueBasis basis = RevenueBasis.Earned, string? currency = null, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var range = RevenuePeriods.Resolve(period, from, to, now);
            var inXmr = IsXmr(currency);
            var (invoices, oneTime) = await this.LoadInAsync(inXmr, ct);
            var report = RevenueReportBuilder.Build(invoices, oneTime, range.From, range.To, basis, now, decimals: inXmr ? 8 : 2);
            return this.Png(RevenuePlotting.Pie(report, inXmr));
        }

        // Monthly chart: the selected period when it spans 3+ months, otherwise the last 24 months.
        [HttpGet("monthly.png")]
        public async Task<IActionResult> Monthly(string? period, DateTime? from, DateTime? to, RevenueBasis basis = RevenueBasis.Earned, string? currency = null, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var range = RevenuePeriods.Resolve(period, from, to, now);
            var inXmr = IsXmr(currency);
            var (invoices, oneTime) = await this.LoadInAsync(inXmr, ct);

            var months = ChartMonths(invoices, oneTime, range, basis, now);
            return this.Png(RevenuePlotting.MonthlyStacked(months, basis, inXmr));
        }

        // The months shown in the monthly chart (and its table): the selected period when it
        // spans 3+ months, otherwise the last 24 months.
        private static List<RevenueMonth> ChartMonths(
            IReadOnlyList<SponsoredListingInvoice> invoices,
            IReadOnlyList<RevenueItem> oneTime,
            (string Key, string Label, DateTime From, DateTime To) range,
            RevenueBasis basis,
            DateTime now)
        {
            var start = range.From;
            if ((range.To - range.From).TotalDays < 85)
            {
                start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-23);
            }

            var end = range.To > now ? now.Date.AddDays(1) : range.To;
            var months = RevenueReportBuilder.BuildMonths(invoices, oneTime, start, end, basis, now);
            return range.Key == "all" ? RevenueReportBuilder.TrimLeadingEmptyMonths(months) : months;
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> Forecast(int? horizonMonths, int? confidence, decimal? priceGrowth, CancellationToken ct = default)
        {
            var (horizon, conf, growth) = Normalize(horizonMonths, confidence, priceGrowth);
            var f = await this.GetForecastAsync(horizon, conf, growth, ct);

            return this.View(new RevenueForecastViewModel
            {
                HorizonMonths = horizon,
                ConfidencePercent = conf,
                PriceGrowthPercent = growth,
                Result = f.Result,
                History = f.History,
                Advice = f.Advice,
            });
        }

        [HttpGet("forecast.png")]
        public async Task<IActionResult> ForecastChart(int? horizonMonths, int? confidence, decimal? priceGrowth, CancellationToken ct = default)
        {
            var (horizon, conf, growth) = Normalize(horizonMonths, confidence, priceGrowth);
            var f = await this.GetForecastAsync(horizon, conf, growth, ct);
            return this.Png(RevenuePlotting.Forecast(f.History, f.Result));
        }

        [HttpGet("backtest/{monthsBack:int}.png")]
        public async Task<IActionResult> BacktestChart(int monthsBack, int? horizonMonths, int? confidence, decimal? priceGrowth, CancellationToken ct = default)
        {
            var (horizon, conf, growth) = Normalize(horizonMonths, confidence, priceGrowth);
            var result = (await this.GetForecastAsync(horizon, conf, growth, ct)).Result;
            var bt = result.Backtests.FirstOrDefault(b =>
                ((DateTime.UtcNow.Year - b.AsOfUtc.Year) * 12) + DateTime.UtcNow.Month - b.AsOfUtc.Month == monthsBack);
            return bt is null ? this.NotFound() : this.Png(RevenuePlotting.Backtest(bt, result.Confidence));
        }

        private static (int Horizon, int Confidence, decimal PriceGrowth) Normalize(int? horizonMonths, int? confidence, decimal? priceGrowth) =>
            (Math.Clamp(horizonMonths ?? 12, 1, 24), Math.Clamp(confidence ?? 80, 50, 95), Math.Clamp(priceGrowth ?? 0m, -50m, 200m));

        private async Task<ForecastBundle> GetForecastAsync(int horizon, int confidencePct, decimal priceGrowthPct, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var key = $"{ForecastCacheKeyPrefix}{horizon}:{confidencePct}:{priceGrowthPct.ToString(CultureInfo.InvariantCulture)}:{now.ToString(DirectoryManager.Common.Constants.StringConstants.CompactDateHourFormat)}";
            if (this.cache.TryGetValue(key, out ForecastBundle? cached) && cached != null)
            {
                return cached;
            }

            var (invoices, oneTime) = await this.LoadAsync(ct);
            var subcategories = await this.subcategoryRepository.GetAllAsync();
            var categories = await this.categoryRepository.GetAllAsync();

            var result = SponsorshipForecastModel.Run(
                invoices,
                oneTime,
                new SponsorshipForecastModel.Options
                {
                    AsOfUtc = now,
                    HorizonMonths = horizon,
                    Confidence = confidencePct / 100.0,
                    PriceGrowthPerYear = (double)priceGrowthPct / 100.0,
                    MainSlots = DirectoryManager.Common.Constants.IntegerConstants.MaxMainSponsoredListings,
                    CategorySlots = categories.Count() * DirectoryManager.Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
                    SubcategorySlots = subcategories.Count(s => !s.SponsorshipDisabled)
                                       * DirectoryManager.Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
                },
                now);

            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var history = RevenueReportBuilder.BuildMonths(
                invoices, oneTime, monthStart.AddMonths(-18), monthStart, RevenueBasis.Earned, now);

            var analysis = SponsorshipIncomeAnalyzer.Analyze(invoices, Currency.USD, now);
            var value = new ForecastBundle(result, history, RevenueAdvice.Build(result, analysis));
            this.cache.Set(key, value, ForecastCacheFor);
            return value;
        }

        private static bool IsXmr(string? currency) =>
            string.Equals(currency, "xmr", StringComparison.OrdinalIgnoreCase);

        private static (List<SponsoredListingInvoice> Invoices, List<RevenueItem> OneTime) ToXmr(
            List<SponsoredListingInvoice> invoices, List<RevenueItem> oneTime) =>
            (XmrRevenueConverter.ConvertInvoices(invoices), XmrRevenueConverter.ConvertItems(oneTime));

        // Revenue inputs in the selected unit (XMR amounts replace the USD ones when inXmr).
        private async Task<(List<SponsoredListingInvoice> Invoices, List<RevenueItem> OneTime)> LoadInAsync(bool inXmr, CancellationToken ct)
        {
            var (invoices, oneTime) = await this.LoadAsync(ct);
            return inXmr ? ToXmr(invoices, oneTime) : (invoices, oneTime);
        }

        private async Task<(List<SponsoredListingInvoice> Invoices, List<RevenueItem> OneTime)> LoadAsync(CancellationToken ct)
        {
            var invoices = (await this.invoiceRepository.GetAllAsync())
                .Where(i => i.PaymentStatus == PaymentStatus.Paid || i.PaymentStatus == PaymentStatus.OverPayment)
                .ToList();
            var oneTime = await this.revenueRepository.GetOneTimeRevenueAsync(ct);
            return (invoices, oneTime);
        }

        private sealed record ForecastBundle(
            SponsorshipForecastModel.Result Result,
            List<RevenueMonth> History,
            List<ForecastAdvice> Advice);

        private FileContentResult Png(byte[] bytes)
        {
            this.Response.Headers.CacheControl = "private, max-age=60";
            return this.File(bytes, "image/png");
        }
    }
}
