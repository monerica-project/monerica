using System.Globalization;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Reports;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Net.Http.Headers;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SponsoredListingInvoiceController : Controller
    {
        private readonly ISponsoredListingInvoiceRepository invoiceRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICacheService cacheService;

        public SponsoredListingInvoiceController(
            ISponsoredListingInvoiceRepository invoiceRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            ICacheService cacheService)
        {
            this.invoiceRepository = invoiceRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.cacheService = cacheService;
        }

        [Route("sponsoredlistinginvoice")]
        [HttpGet]
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = IntegerConstants.DefaultPageSize)
        {
            var (invoices, totalItems) = await this.invoiceRepository
                                               .GetPageAsync(page, pageSize)
                                               .ConfigureAwait(false);

            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalItems = totalItems;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.IsPaidOnly = false;

            return this.View(invoices);
        }

        [Route("sponsoredlistinginvoice/paid")]
        [HttpGet]
        public async Task<IActionResult> PaidIndex(int page = 1, int pageSize = IntegerConstants.DefaultPageSize)
        {
            var (invoices, totalItems) = await this.invoiceRepository
                                               .GetPageByTypeAsync(page, pageSize, PaymentStatus.Paid)
                                               .ConfigureAwait(false);

            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalItems = totalItems;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.IsPaidOnly = true;

            return this.View("Index", invoices);
        }

        [Route("sponsoredlistinginvoice/details")]
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return this.NotFound();
            }

            var invoice = await this.invoiceRepository
                                   .GetByIdAsync(id.Value)
                                   .ConfigureAwait(false);
            if (invoice == null)
            {
                return this.NotFound();
            }

            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var entry = await this.directoryEntryRepository
                                   .GetByIdAsync(invoice.DirectoryEntryId)
                                   .ConfigureAwait(false);
            if (entry == null)
            {
                return this.NotFound();
            }

            if (invoice.SponsorshipType == SponsorshipType.SubcategorySponsor
                && invoice.SubCategoryId.HasValue)
            {
                var subcategory = (await this.subCategoryRepository
                                             .GetAllActiveSubCategoriesAsync()
                                             .ConfigureAwait(false))
                                  .OrderBy(sc => sc.Category.Name)
                                  .ThenBy(sc => sc.Name)
                                  .FirstOrDefault(sc => sc.SubCategoryId == invoice.SubCategoryId.Value);
                this.ViewBag.Subcategory = subcategory;
            }
            else if (invoice.SponsorshipType == SponsorshipType.CategorySponsor
                     && invoice.CategoryId.HasValue)
            {
                var category = await this.categoryRepository
                                           .GetByIdAsync(invoice.CategoryId.Value)
                                           .ConfigureAwait(false);
                this.ViewBag.Category = category;
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel
            {
                CreateDate = entry.CreateDate,
                UpdateDate = entry.UpdateDate,
                DateOption = DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = entry.Link,
                Name = entry.Name,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                Contact = entry.Contact,
                Description = entry.Description,
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryStatus = entry.DirectoryStatus,
                Link2 = entry.Link2,
                Link3 = entry.Link3,
                Location = entry.Location,
                Note = entry.Note,
                Processor = entry.Processor,
                SubCategoryId = entry.SubCategoryId,
                CountryCode = entry.CountryCode,
                PgpKey = entry.PgpKey
            };

            return this.View(invoice);
        }

        [Route("report")]
        [HttpGet]
        public async Task<IActionResult> Report(
            DateTime? startDate,
            DateTime? endDate,
            SponsorshipType? sponsorshipType,
            Currency? displayCurrency,
            int? subCategoryId)
        {
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var fromDate = startDate?.Date ?? now.AddYears(-defaultYears).Date;
            var toDate = endDate?.Date ?? now.Date;

            var model = new InvoiceQueryViewModel
            {
                StartDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 23, 59, 59, DateTimeKind.Utc),
                SponsorshipType = sponsorshipType,
                DisplayCurrency = displayCurrency ?? Currency.USD,
                SubCategoryId = subCategoryId
            };

            // Sponsorship type dropdown
            model.SponsorshipTypeOptions = Enum.GetValues(typeof(SponsorshipType))
                .Cast<SponsorshipType>()
                .Where(st => st != SponsorshipType.Unknown)
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = st.ToString(),
                    Selected = sponsorshipType.HasValue && sponsorshipType.Value == st
                })
                .Prepend(new SelectListItem { Value = "", Text = "All", Selected = !sponsorshipType.HasValue })
                .ToList();

            // Currency dropdown
            model.DisplayCurrencyOptions = Enum.GetValues(typeof(Currency))
                .Cast<Currency>()
                .Where(c => c != Currency.Unknown)
                .Select(c => new SelectListItem
                {
                    Value = c.ToString(),
                    Text = c.ToString(),
                    Selected = c == model.DisplayCurrency
                })
                .ToList();

            // Subcategory dropdown
            var subs = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false);
            model.SubCategoryOptions = subs
                .OrderBy(s => s.Category.Name).ThenBy(s => s.Name)
                .Select(s => new SelectListItem
                {
                    Value = s.SubCategoryId.ToString(),
                    Text = $"{s.Category.Name} > {s.Name}",
                    Selected = subCategoryId.HasValue && s.SubCategoryId == subCategoryId.Value
                })
                .Prepend(new SelectListItem { Value = "", Text = "All Subcategories", Selected = !subCategoryId.HasValue })
                .ToList();

            // Fetch and filter paid invoices in date window
            var allInvoices = await this.invoiceRepository.GetAllAsync().ConfigureAwait(false);
            var filtered = allInvoices.Where(inv =>
                inv.CreateDate >= model.StartDate &&
                inv.CreateDate <= model.EndDate &&
                inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                filtered = filtered.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            if (subCategoryId.HasValue)
            {
                filtered = filtered.Where(inv => inv.SubCategoryId == subCategoryId.Value);
            }

            // Total in selected currency (generic)
            model.TotalInDisplayCurrency = filtered.Sum(inv => inv.AmountIn(model.DisplayCurrency));

            // Keep legacy fields if needed elsewhere
            model.TotalPaidAmount = filtered.Sum(inv => inv.PaidAmount);
            model.TotalAmount = filtered.Sum(inv => inv.Amount);
            model.PaidInCurrency = filtered.Select(inv => inv.PaidInCurrency).FirstOrDefault();
            model.Currency = filtered.Select(inv => inv.Currency).FirstOrDefault();

            // --- FX lines ---
            if (model.DisplayCurrency == Currency.USD)
            {
                var sumUsd = filtered.Sum(i => i.AmountIn(Currency.USD));
                var sumXmr = filtered.Sum(i => i.AmountIn(Currency.XMR));

                if (sumUsd > 0m && sumXmr > 0m)
                {
                    model.AverageUsdPerXmr = sumUsd / sumXmr;
                    model.ImpliedTotalInXmrAtAvg = model.TotalInDisplayCurrency / model.AverageUsdPerXmr;
                }
                else
                {
                    model.AverageUsdPerXmr = null;
                    model.ImpliedTotalInXmrAtAvg = null;
                }

                model.AverageUsdPerUnitForDisplayCurrency = null; // not shown in USD mode
            }
            else
            {
                var sumUsd = filtered.Sum(i => i.AmountIn(Currency.USD));
                var sumCur = filtered.Sum(i => i.AmountIn(model.DisplayCurrency));

                model.AverageUsdPerUnitForDisplayCurrency =
                    (sumUsd > 0m && sumCur > 0m) ? sumUsd / sumCur : (decimal?)null;

                model.AverageUsdPerXmr = null;
                model.ImpliedTotalInXmrAtAvg = null;
            }

            // ----- Prepaid FUTURE services (apply same higher-level filters) -----
            var asOfUtc = DateTime.UtcNow.Date;
            var futurePaid = allInvoices.Where(inv => inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                futurePaid = futurePaid.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            if (subCategoryId.HasValue)
            {
                futurePaid = futurePaid.Where(inv => inv.SubCategoryId == subCategoryId.Value);
            }

            decimal futureRevenue = 0m;
            DateTime? paidThrough = null;
            var intervals = new List<(DateTime S, DateTime E)>();

            foreach (var inv in futurePaid)
            {
                var start = inv.CampaignStartDate.Date;
                var end = inv.CampaignEndDate.Date;
                if (end < start)
                {
                    continue;
                }

                var os = start < asOfUtc ? asOfUtc : start;
                var oe = end;
                if (oe < os)
                {
                    continue;
                }

                intervals.Add((os, oe));

                var totalDays = (decimal)InclusiveDays(start, end); // FIXED
                if (totalDays > 0m)
                {
                    var overlapDays = (decimal)OverlapInclusiveDays(start, end, os, oe.AddDays(1));
                    var perDay = inv.AmountIn(model.DisplayCurrency) / totalDays;
                    futureRevenue += perDay * overlapDays;
                }

                if (!paidThrough.HasValue || end > paidThrough.Value)
                {
                    paidThrough = end;
                }
            }

            static int CountDistinctDays(List<(DateTime S, DateTime E)> ivals)
            {
                if (ivals.Count == 0) return 0;

                ivals.Sort((a, b) => a.S.CompareTo(b.S));
                var curS = ivals[0].S;
                var curE = ivals[0].E;
                long total = 0;
                for (int i = 1; i < ivals.Count; i++)
                {
                    var (s, e) = ivals[i];
                    if (s <= curE.AddDays(1))
                    {
                        if (e > curE)
                        {
                            curE = e;
                        }
                    }
                    else
                    {
                        total += InclusiveDays(curS, curE);
                        curS = s;
                        curE = e;
                    }
                }

                total += InclusiveDays(curS, curE);
                return (int)total;
            }

            model.FutureRevenueInDisplayCurrency = futureRevenue;
            model.PaidThroughDateUtc = paidThrough;
            model.FutureServiceDaysDistinct = CountDistinctDays(intervals);
            model.FutureServiceDaysContinuous =
                paidThrough.HasValue && paidThrough.Value >= asOfUtc
                    ? InclusiveDays(asOfUtc, paidThrough.Value)
                    : 0;

            return this.View(model);
        }

        [HttpGet("sponsoredlistinginvoice/monthlyincomebarchart")]
        public async Task<IActionResult> MonthlyIncomeBarChart(
            DateTime startDate,
            DateTime endDate,
            SponsorshipType? sponsorshipType,
            Currency? displayCurrency,
            int? subCategoryId)
        {
            var currency = displayCurrency ?? Currency.USD;
            var invoices = await this.invoiceRepository.GetAllAsync();

            var filtered = invoices.Where(inv =>
                inv.PaymentStatus == PaymentStatus.Paid &&
                inv.CreateDate.Date >= startDate.Date &&
                inv.CreateDate.Date <= endDate.Date);

            if (sponsorshipType.HasValue)
            {
                filtered = filtered.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            if (subCategoryId.HasValue)
            {
                filtered = filtered.Where(inv => inv.SubCategoryId == subCategoryId.Value);
            }

            var list = filtered.ToList();
            if (!list.Any())
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='20' fill='black'>No results</text>
</svg>";
                return this.File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            var filterLabel = await this.BuildFilterLabelAsync(sponsorshipType, subCategoryId);
            var imageBytes = new InvoicePlotting()
                .CreateMonthlyIncomeBarChart(list, currency, filterLabel);

            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("sponsoredlistinginvoice/monthlyavgdailyrevenuechart")]
        public async Task<IActionResult> MonthlyAvgDailyRevenueChart(
            DateTime startDate,
            DateTime endDate,
            SponsorshipType? sponsorshipType,
            Currency? displayCurrency,
            int? subCategoryId)
        {
            var currency = displayCurrency ?? Currency.USD;

            var monthStart = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEndUI = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var invoices = await this.invoiceRepository.GetAllAsync();
            var paid = invoices.Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                paid = paid.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            if (subCategoryId.HasValue)
            {
                paid = paid.Where(i => i.SubCategoryId == subCategoryId.Value);
            }

            var list = paid.ToList();
            if (!list.Any())
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='20' fill='black'>No results</text>
</svg>";
                return this.File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            var paidThrough = list.Max(i => i.CampaignEndDate.Date);
            var paidThroughMonth = new DateTime(paidThrough.Year, paidThrough.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = paidThroughMonth > monthEndUI ? paidThroughMonth : monthEndUI;

            var filterLabel = await this.BuildFilterLabelAsync(sponsorshipType, subCategoryId);

            var bytes = new InvoicePlotting()
                .CreateMonthlyAvgDailyRevenueChart(list, currency, monthStart, monthEnd, filterLabel);

            return this.File(bytes, StringConstants.PngImage);
        }

        // --- Subcategory Revenue Pie ---
        [AllowAnonymous]
        [HttpGet("sponsoredlistinginvoice/subcategory-revenue-pie")]
        public async Task<IActionResult> SubcategoryRevenuePieChart(
            DateTime? startDate,
            DateTime? endDate,
            SponsorshipType? sponsorshipType,
            Currency? displayCurrency)
        {
            var currency = displayCurrency ?? Currency.USD;

            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = (startDate ?? now.AddYears(-defaultYears)).Date;
            var to = (endDate ?? now).Date;

            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc);

            var allPaid = (await this.invoiceRepository.GetAllAsync().ConfigureAwait(false))
                .Where(inv => inv.CreateDate >= startUtc
                           && inv.CreateDate <= endUtc
                           && inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                allPaid = allPaid.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            var matches = allPaid.Where(inv => inv.MatchesCurrency(currency)).ToList();

            var catsList = await this.categoryRepository.GetAllAsync().ConfigureAwait(false);
            var cats = catsList.ToDictionary(c => c.CategoryId, c => c.Name);

            var subsList = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false);
            var subs = subsList.ToDictionary(s => s.SubCategoryId, s => s.Name);
            var subToCat = subsList.ToDictionary(s => s.SubCategoryId, s => s.CategoryId);

            var chartBytes = new InvoicePlotting().CreateSubcategoryRevenuePieChart(matches, cats, subs, subToCat, currency);

            if (chartBytes == null || chartBytes.Length == 0)
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='20' fill='black'>No results</text>
</svg>";
                return this.File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            return this.File(chartBytes, StringConstants.PngImage);
        }

        [Route("sponsoredlistinginvoice/subcategorybreakdown")]
        [HttpGet]
        public async Task<IActionResult> SubcategoryBreakdown(DateTime? startDate, DateTime? endDate)
        {
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = startDate?.Date ?? now.AddYears(-defaultYears).Date;
            var to = endDate?.Date ?? now.Date;

            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc);

            // 1) fetch & filter paid invoices in range
            var allPaid = (await this.invoiceRepository.GetAllAsync().ConfigureAwait(false))
                .Where(inv => inv.CreateDate >= startUtc
                           && inv.CreateDate <= endUtc
                           && inv.PaymentStatus == PaymentStatus.Paid)
                .ToList();

            // 2) load lookup maps
            var catsList = await this.categoryRepository.GetAllAsync().ConfigureAwait(false);
            var cats = catsList.ToDictionary(c => c.CategoryId, c => c.Name);

            var subsList = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false);
            var subsFull = subsList.ToDictionary(
                s => s.SubCategoryId,
                s => $"{cats[s.CategoryId]} > {s.Name}");

            // 3) MAIN-SPONSOR breakdown by SubCategoryId, revenue + count
            var mainSet = allPaid.Where(i => i.SponsorshipType == SponsorshipType.MainSponsor && i.SubCategoryId.HasValue);
            var mainTotalRevenue = mainSet.Sum(i => i.Amount);

            var mainGroups = mainSet
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    var rev = g.Sum(inv => inv.Amount);
                    var cnt = g.Count();
                    var name = subsFull.TryGetValue(g.Key, out var n) ? n : $"(Unknown {g.Key})";
                    var pct = mainTotalRevenue > 0
                                ? Math.Round(rev * 100m / mainTotalRevenue, 2)
                                : 0m;
                    return new BreakdownRow
                    {
                        Name = name,
                        Revenue = rev,
                        Count = cnt,
                        Percentage = pct
                    };
                })
                .OrderByDescending(r => r.Revenue)
                .ToList();

            // 4) SUBCATEGORY-SPONSOR breakdown by SubCategoryId
            var subSet = allPaid.Where(i => i.SponsorshipType == SponsorshipType.SubcategorySponsor && i.SubCategoryId.HasValue);
            var subTotalRevenue = subSet.Sum(i => i.Amount);

            var subGroups = subSet
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    var rev = g.Sum(inv => inv.Amount);
                    var cnt = g.Count();
                    var name = subsFull.TryGetValue(g.Key, out var n) ? n : $"(Unknown {g.Key})";
                    var pct = subTotalRevenue > 0
                               ? Math.Round(rev * 100m / subTotalRevenue, 2)
                               : 0m;
                    return new BreakdownRow
                    {
                        Name = name,
                        Revenue = rev,
                        Count = cnt,
                        Percentage = pct
                    };
                })
                .OrderByDescending(r => r.Revenue)
                .ToList();

            // 5) CATEGORY-SPONSOR breakdown by SubCategoryId
            var catSet = allPaid.Where(i => i.SponsorshipType == SponsorshipType.CategorySponsor && i.SubCategoryId.HasValue);
            var catTotalRevenue = catSet.Sum(i => i.Amount);

            var catGroups = catSet
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    var rev = g.Sum(inv => inv.Amount);
                    var cnt = g.Count();
                    var name = subsFull.TryGetValue(g.Key, out var n) ? n : $"(Unknown {g.Key})";
                    var pct = catTotalRevenue > 0
                               ? Math.Round(rev * 100m / catTotalRevenue, 2)
                               : 0m;
                    return new BreakdownRow
                    {
                        Name = name,
                        Revenue = rev,
                        Count = cnt,
                        Percentage = pct
                    };
                })
                .OrderByDescending(r => r.Revenue)
                .ToList();

            // 6) wrap in view‐model
            var vm = new BreakdownReportViewModel
            {
                StartDate = startUtc,
                EndDate = endUtc,
                MainSponsorBreakdown = mainGroups,
                SubcategoryBreakdown = subGroups,
                CategoryBreakdown = catGroups
            };

            return this.View(vm);
        }

        [HttpGet("sponsoredlistinginvoice/download-accountant-csv")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DownloadAccountantCsv(
        DateTime? startDate,
        DateTime? endDate,
        SponsorshipType? sponsorshipType,
        bool costEqualsSalesPrice = true)
        {
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = (startDate ?? now.AddYears(-defaultYears)).Date;
            var to = (endDate ?? now).Date;

            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc);

            var fileName = $"Accountant_Crypto_PAID_{from:yyyyMMdd}_to_{to:yyyyMMdd}.csv";

            this.Response.ContentType = "text/csv; charset=utf-8";
            this.Response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"{fileName}\"";
            this.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            this.Response.Headers.Pragma = "no-cache";
            this.Response.Headers["X-Download-Options"] = "noopen";

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bom = utf8.GetPreamble();
            if (bom.Length > 0)
            {
                await this.Response.Body.WriteAsync(bom, 0, bom.Length);
            }

            await using var writer = new StreamWriter(this.Response.Body, utf8, bufferSize: 64 * 1024, leaveOpen: true);

            static string D(DateTime utc) => utc.ToUniversalTime().ToString("MM/dd/yy", CultureInfo.InvariantCulture);
            static string N8(decimal v) => v.ToString("0.########", CultureInfo.InvariantCulture);
            static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            await writer.WriteLineAsync("Quantity,Description,Sales Date,Purchase Date,Sales Price,Cost");

            try
            {
                int batch = 0;
                await foreach (var r in this.invoiceRepository.StreamPaidForAccountantAsync(startUtc, endUtc, sponsorshipType))
                {
                    var sales = r.SalesPrice;
                    var cost = costEqualsSalesPrice ? r.SalesPrice : 0m;

                    await writer.WriteLineAsync(string.Join(
                        ",",
                        N8(r.Quantity),
                        r.Description,
                        D(r.PaidDateUtc),
                        D(r.PaidDateUtc),
                        Money(sales),
                        Money(cost)));

                    if (++batch % 200 == 0)
                    {
                        await writer.FlushAsync();
                    }
                }

                await writer.FlushAsync();
            }
            catch (OperationCanceledException)
            { /* client canceled */ }
            catch (IOException)
            { /* broken pipe */ }

            return new EmptyResult();
        }

        [Route("sponsoredlistinginvoice/advertiserbreakdown")]
        [HttpGet]
        public async Task<IActionResult> AdvertiserBreakdown(
            DateTime? startDate,
            DateTime? endDate,
            SponsorshipType? sponsorshipType)
        {
            // Reporting window: half-open [start, endOpen)
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = (startDate ?? now.AddYears(-defaultYears)).Date;
            var to = (endDate ?? now).Date;

            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endOpenUtc = new DateTime(to.Year, to.Month, to.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);

            // Exclusive purchased days (minimum 1)
            static int ExclusivePurchasedDays(DateTime start, DateTime end)
            {
                var s = start.Date;
                var e = end.Date; // exclusive
                var d = (int)(e - s).TotalDays;
                return Math.Max(1, d);
            }

            // 1) Load invoices; PAID filter + optional sponsorship type
            var all = await this.invoiceRepository.GetAllAsync().ConfigureAwait(false);
            var paid = all.Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                paid = paid.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            // 2) Keep only invoices whose CreateDate is inside the window (full recognition by invoice)
            var inWindow = paid.Where(i => i.CreateDate >= startUtc && i.CreateDate < endOpenUtc);

            // 3) Group by advertiser; sum full invoice Amount and sum purchased days (no proration)
            var grouped = inWindow
                .GroupBy(i => new { i.DirectoryEntryId, i.DirectoryEntry.Name })
                .Select(g =>
                {
                    var revenue = g.Sum(x => x.Amount);
                    var daysSum = g.Sum(x => ExclusivePurchasedDays(x.CampaignStartDate, x.CampaignEndDate));
                    var count = g.Count();

                    var revenueRounded = Math.Round(revenue, 2, MidpointRounding.AwayFromZero);
                    var avgPerDay = daysSum > 0
                        ? Math.Round(revenueRounded / daysSum, 2, MidpointRounding.AwayFromZero)
                        : 0m;

                    return new
                    {
                        g.Key.DirectoryEntryId,
                        g.Key.Name,
                        RevenueRounded = revenueRounded,
                        AvgPerDay = avgPerDay,
                        Count = count,
                        DaysSum = daysSum
                    };
                })
                .OrderByDescending(x => x.RevenueRounded)
                .ToList();

            var totalRevenueRounded = grouped.Sum(x => x.RevenueRounded);

            var rows = grouped.Select(x =>
                new AdvertiserBreakdownRow
                {
                    DirectoryEntryId = x.DirectoryEntryId,
                    DirectoryEntryName = x.Name,
                    Revenue = x.RevenueRounded,
                    Count = x.Count,
                    AveragePerDay = x.AvgPerDay,
                    Percentage = totalRevenueRounded > 0m
                        ? Math.Round(x.RevenueRounded * 100m / totalRevenueRounded, 2, MidpointRounding.AwayFromZero)
                        : 0m
                })
                .ToList();

            var options = Enum.GetValues(typeof(SponsorshipType))
                .Cast<SponsorshipType>()
                .Where(st => st != SponsorshipType.Unknown)
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = st.ToString(),
                    Selected = sponsorshipType.HasValue && sponsorshipType.Value == st
                })
                .Prepend(new SelectListItem { Value = "", Text = "All", Selected = !sponsorshipType.HasValue })
                .ToList();

            var vm = new AdvertiserBreakdownViewModel
            {
                StartDate = from,
                EndDate = to,
                SponsorshipType = sponsorshipType,
                SponsorshipTypeOptions = options,
                Rows = rows
            };

            return this.View(vm);
        }

   
        [Route("sponsoredlistinginvoice/advertiser")]
        [HttpGet]
        public async Task<IActionResult> Advertiser(int directoryEntryId, int page = 1, int pageSize = int.MaxValue)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (entry == null)
            {
                return this.NotFound();
            }

            // Pull all invoices for this advertiser (all-time), then filter to PAID and page in-memory
            var (allInvoicesForEntry, _) = await this.invoiceRepository
                .GetInvoicesForDirectoryEntryAsync(directoryEntryId, page: 1, pageSize: int.MaxValue);

            var paid = allInvoicesForEntry
                .Where(i => i.PaymentStatus == PaymentStatus.Paid)
                .OrderByDescending(i => i.CreateDate)
                .ToList();

            var total = paid.Count;

            // ---- exclusive day math helper (min 1) ----
            static int ExclusivePurchasedDays(DateTime start, DateTime end)
            {
                var s = start.Date;
                var e = end.Date; // exclusive
                var d = (int)(e - s).TotalDays;
                return Math.Max(1, d);
            }

            // Totals for summary
            var totalPurchasedDays = paid.Sum(i => ExclusivePurchasedDays(i.CampaignStartDate, i.CampaignEndDate));
            var totalPaidAllTime = paid.Sum(i => i.Amount);

            var avgPerDayAllTime = totalPurchasedDays > 0
                ? Math.Round(totalPaidAllTime / totalPurchasedDays, 2, MidpointRounding.AwayFromZero)
                : 0m;

            // Page the rows
            var items = paid
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i =>
                {
                    var days = ExclusivePurchasedDays(i.CampaignStartDate, i.CampaignEndDate);
                    var avg = Math.Round((double)(i.Amount / (decimal)days), 2, MidpointRounding.AwayFromZero);

                    return new DirectoryManager.Web.Models.Reports.AdvertiserInvoiceRow
                    {
                        SponsoredListingInvoiceId = i.SponsoredListingInvoiceId,
                        Amount = i.Amount,
                        Currency = i.Currency.ToString(),
                        CampaignStartDate = i.CampaignStartDate,
                        CampaignEndDate = i.CampaignEndDate,
                        AvgUsdPerDay = avg,
                        DaysPurchased = days,
                        SponsorshipType = i.SponsorshipType.ToString(),
                        PaymentStatus = i.PaymentStatus.ToString(),
                        CreateDate = i.CreateDate,
                    };
                })
                .ToList();

            var model = new DirectoryManager.Web.Models.Reports.AdvertiserInvoiceListViewModel
            {
                DirectoryEntryId = directoryEntryId,
                DirectoryEntryName = entry.Name,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPaidAllTime = totalPaidAllTime,
                TotalPurchasedDaysExclusive = totalPurchasedDays,
                AverageUsdPerDayAllTime = avgPerDayAllTime,
                Rows = items
            };

            return this.View("AdvertiserInvoices", model);
        }

        // ---- helper for distinct calendar days across intervals (inclusive) ----
        private static int CountDistinctDays(List<(DateTime S, DateTime E)> ivals)
        {
            if (ivals == null || ivals.Count == 0)
            {
                return 0;
            }

            ivals.Sort((a, b) => a.S.CompareTo(b.S));

            var curS = ivals[0].S;
            var curE = ivals[0].E;
            long total = 0;

            for (int i = 1; i < ivals.Count; i++)
            {
                var (s, e) = ivals[i];
                // Merge if touching or overlapping (inclusive days)
                if (s <= curE.AddDays(1))
                {
                    if (e > curE)
                    {
                        curE = e;
                    }
                }
                else
                {
                    total += InclusiveDays(curS, curE);
                    curS = s;
                    curE = e;
                }
            }

            total += InclusiveDays(curS, curE);
            return (int)total;
        }

        // ---------- DAY-COUNT HELPERS (fix off-by-one) ----------
        private static int InclusiveDays(DateTime startUtc, DateTime endUtc)
        {
            // counts both start and end dates; clamps to >= 0
            var s = startUtc.Date;
            var eOpen = endUtc.Date.AddDays(1);
            var days = (int)(eOpen - s).TotalDays;
            return Math.Max(0, days);
        }

        private static int OverlapInclusiveDays(
            DateTime campaignStartUtc,
            DateTime campaignEndUtc,
            DateTime windowStartUtc,
            DateTime windowEndOpenUtc)
        {
            // clip campaign [start, end] (inclusive) to window [winStart, winEnd) (end-exclusive)
            var s = campaignStartUtc.Date;
            var eOpen = campaignEndUtc.Date.AddDays(1); // inclusive -> end-open
            var a = s > windowStartUtc.Date ? s : windowStartUtc.Date;
            var b = eOpen < windowEndOpenUtc.Date ? eOpen : windowEndOpenUtc.Date;

            var days = (int)(b - a).TotalDays; // already end-exclusive
            return Math.Max(0, days);
        }

        private static string FriendlySponsorship(SponsorshipType? st) =>
            st switch
            {
                SponsorshipType.MainSponsor => "Main Sponsor",
                SponsorshipType.CategorySponsor => "Category Sponsor",
                SponsorshipType.SubcategorySponsor => "Subcategory Sponsor",
                null => "All",
                _ => st?.ToString() ?? "All"
            };

        private async Task<string> BuildFilterLabelAsync(SponsorshipType? sponsorshipType, int? subCategoryId)
        {
            string left = FriendlySponsorship(sponsorshipType);

            if (!subCategoryId.HasValue)
            {
                return $"{left} : All";
            }

            var sub = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false))
                        .FirstOrDefault(s => s.SubCategoryId == subCategoryId.Value);

            if (sub is null)
            {
                return $"{left} : (Unknown {subCategoryId.Value})";
            }

            string right = $"{sub.Category?.Name ?? "Unknown"} > {sub.Name}";
            return $"{left} : {right}";
        }
    }
}