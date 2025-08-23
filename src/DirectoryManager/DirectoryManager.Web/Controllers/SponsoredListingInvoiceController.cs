using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Reports;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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
        public async Task<IActionResult> Index(int page = 1, int pageSize = IntegerConstants.DefaultPageSize)
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

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

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
            SponsorshipType? sponsorshipType)
        {
            // 1) Normalize your dates
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var fromDate = startDate?.Date ?? now.AddYears(-defaultYears).Date;
            var toDate = endDate?.Date ?? now.Date;

            var model = new InvoiceQueryViewModel
            {
                StartDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 23, 59, 59, DateTimeKind.Utc),
                SponsorshipType = sponsorshipType
            };

            // 2) Build your “All + each enum” dropdown
            model.SponsorshipTypeOptions = Enum.GetValues(typeof(SponsorshipType))
                .Cast<SponsorshipType>()
                .Where(st => st != SponsorshipType.Unknown)
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = st.ToString(),
                    Selected = sponsorshipType.HasValue && sponsorshipType.Value == st
                })
                .Prepend(new SelectListItem
                {
                    Value = "",
                    Text = "All",
                    Selected = !sponsorshipType.HasValue
                })
                .ToList();

            // 3) Fetch & filter the raw invoices
            var allInvoices = await this.invoiceRepository.GetAllAsync().ConfigureAwait(false);
            var filtered = allInvoices
                .Where(inv => inv.CreateDate >= model.StartDate
                           && inv.CreateDate <= model.EndDate
                           && inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                filtered = filtered
                    .Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            // 4) Build a little “result” that mirrors your old repository DTO
            var result = new
            {
                TotalReceivedAmount = filtered.Sum(inv => inv.PaidAmount),
                TotalAmount = filtered.Sum(inv => inv.Amount),
                Currency = filtered.Select(inv => inv.Currency)
                                              .FirstOrDefault(),
                PaidInCurrency = filtered.Select(inv => inv.PaidInCurrency)
                                              .FirstOrDefault()
            };

            // 5) Exactly the assignments you had before:
            model.TotalPaidAmount = result.TotalReceivedAmount;
            model.Currency = result.Currency;
            model.TotalAmount = result.TotalAmount;
            model.PaidInCurrency = result.PaidInCurrency;

            return this.View(model);
        }

        [HttpGet("sponsoredlistinginvoice/monthlyincomebarchart")]
        public async Task<IActionResult> MonthlyIncomeBarChartAsync(
            DateTime startDate,
            DateTime endDate,
            SponsorshipType? sponsorshipType)
        {
            var invoices = await this.invoiceRepository.GetAllAsync();

            var filtered = invoices
                .Where(inv => inv.CreateDate >= startDate
                           && inv.CreateDate <= endDate)
                .Where(inv => inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                filtered = filtered
                    .Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            if (!filtered.Any())
            {
                // Simple SVG fallback — any <img> tag will render this inline
                const string svg = @"
<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='20' fill='black'>
    No results
  </text>
</svg>";
                byte[] bytes = Encoding.UTF8.GetBytes(svg);
                return this.File(bytes, "image/svg+xml");
            }

            var imageBytes = new InvoicePlotting()
                                 .CreateMonthlyIncomeBarChart(filtered);

            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("sponsoredlistinginvoice/monthlyavgdailyrevenuechart")]
        public async Task<IActionResult> MonthlyAvgDailyRevenueChartAsync(
                DateTime startDate,
                DateTime endDate,
                SponsorshipType? sponsorshipType)
        {
            var invoices = await this.invoiceRepository.GetAllAsync();
            var filtered = invoices
                .Where(inv => inv.CreateDate >= startDate
                           && inv.CreateDate <= endDate
                           && inv.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                filtered = filtered.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            if (!filtered.Any())
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle' font-family='sans-serif' font-size='20' fill='black'>No results</text>
</svg>";
                return this.File(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            var imageBytes = new InvoicePlotting()
                                 .CreateMonthlyAvgDailyRevenueChart(filtered);
            return this.File(imageBytes, StringConstants.PngImage);
        }

        [AllowAnonymous]
        [HttpGet("sponsoredlistinginvoice/subcategory-revenue-pie")]
        public async Task<IActionResult> SubcategoryRevenuePieChart(
            DateTime? startDate,
            DateTime? endDate,
            SponsorshipType? sponsorshipType)
        {
            // 1) normalize dates (default to last 12 months)
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = (startDate ?? now.AddYears(-defaultYears)).Date;
            var to = (endDate ?? now).Date;
            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc);

            // 2) fetch & filter paid invoices in range
            var allPaid = (await this.invoiceRepository.GetAllAsync().ConfigureAwait(false))
                .Where(inv => inv.CreateDate >= startUtc
                           && inv.CreateDate <= endUtc
                           && inv.PaymentStatus == PaymentStatus.Paid)
                .ToList();

            // 2a) if user supplied a sponsorshipType, filter by it
            if (sponsorshipType.HasValue)
            {
                allPaid = allPaid
                    .Where(inv => inv.SponsorshipType == sponsorshipType.Value)
                    .ToList();
            }

            // 3) load lookup maps
            var catsList = await this.categoryRepository.GetAllAsync().ConfigureAwait(false);
            var cats = catsList.ToDictionary(c => c.CategoryId, c => c.Name);

            var subsList = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false);
            var subs = subsList.ToDictionary(s => s.SubCategoryId, s => s.Name);

            // 3a) mapping subcategory → categoryId
            var subToCat = subsList.ToDictionary(s => s.SubCategoryId, s => s.CategoryId);

            // 4) delegate to ScottPlot helper
            var chartBytes = new InvoicePlotting()
                .CreateSubcategoryRevenuePieChart(
                    allPaid,
                    cats,
                    subs,
                    subToCat);

            // 5) return as PNG
            return this.File(chartBytes, "image/png");
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

        [Route("sponsoredlistinginvoice/advertiserbreakdown")]
        [HttpGet]
        public async Task<IActionResult> AdvertiserBreakdown(
            DateTime? startDate,
            DateTime? endDate,
            SponsorshipType? sponsorshipType)
        {
            const int defaultYears = 1;
            var now = DateTime.UtcNow;
            var from = startDate?.Date ?? now.AddYears(-defaultYears).Date;
            var to = endDate?.Date ?? now.Date;

            var startUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Utc);

            // Get all invoices
            var allPaid = (await this.invoiceRepository.GetAllAsync().ConfigureAwait(false))
                .Where(inv =>
                    inv.PaymentStatus == PaymentStatus.Paid &&
                    inv.CreateDate >= startUtc &&
                    inv.CreateDate <= endUtc);

            if (sponsorshipType.HasValue)
            {
                allPaid = allPaid.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            var paidList = allPaid.ToList();

            var rows = new List<AdvertiserBreakdownRow>();
            decimal totalRevenue = 0m;

            var grouped = paidList.GroupBy(inv => inv.DirectoryEntryId);
            foreach (var g in grouped)
            {
                decimal advertiserTotal = 0m;
                double totalActiveDays = 0d;
                int invoiceCount = 0;

                foreach (var inv in g)
                {
                    advertiserTotal += inv.Amount;

                    var start = inv.CampaignStartDate.Date;
                    var end = inv.CampaignEndDate.Date;
                    var days = (end - start).TotalDays;
                    if (days <= 0)
                    {
                        days = 1; // Avoid divide-by-zero or bad data
                    }

                    totalActiveDays += days;

                    invoiceCount++;
                }

                if (totalActiveDays <= 0)
                {
                    totalActiveDays = 1;
                }

                var avgPerDay = Math.Round(advertiserTotal / (decimal)totalActiveDays, 2);
                totalRevenue += advertiserTotal;

                var entry = await this.directoryEntryRepository.GetByIdAsync(g.Key);
                rows.Add(new AdvertiserBreakdownRow
                {
                    DirectoryEntryId = g.Key,
                    DirectoryEntryName = entry?.Name ?? $"(#{g.Key})",
                    Revenue = advertiserTotal,
                    Count = invoiceCount,
                    AveragePerDay = avgPerDay
                });
            }

            foreach (var r in rows)
            {
                r.Percentage = totalRevenue > 0
                    ? Math.Round(r.Revenue * 100m / totalRevenue, 2)
                    : 0m;
            }

            rows = rows.OrderByDescending(r => r.Revenue).ToList();

            var options = Enum.GetValues(typeof(SponsorshipType))
                .Cast<SponsorshipType>()
                .Where(st => st != SponsorshipType.Unknown)
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = st.ToString(),
                    Selected = sponsorshipType.HasValue && sponsorshipType.Value == st
                })
                .Prepend(new SelectListItem
                {
                    Value = "",
                    Text = "All",
                    Selected = !sponsorshipType.HasValue
                })
                .ToList();

            var vm = new AdvertiserBreakdownViewModel
            {
                StartDate = startUtc,
                EndDate = endUtc,
                SponsorshipType = sponsorshipType,
                SponsorshipTypeOptions = options,
                Rows = rows
            };

            return this.View(vm);
        }

        [Route("sponsoredlistinginvoice/advertiser")]
        [HttpGet]
        public async Task<IActionResult> Advertiser(int directoryEntryId, int page = 1, int pageSize = 25)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);
            if (entry == null)
            {
                return this.NotFound();
            }

            var (invoices, total) = await this.invoiceRepository
                .GetInvoicesForDirectoryEntryAsync(directoryEntryId, page, pageSize);

            var (allInvoices, _) = await this.invoiceRepository
                .GetInvoicesForDirectoryEntryAsync(directoryEntryId, page: 1, pageSize: int.MaxValue);

            var totalPaidAllTime = allInvoices.Sum(i => i.Amount);

            var model = new DirectoryManager.Web.Models.Reports.AdvertiserInvoiceListViewModel
            {
                DirectoryEntryId = directoryEntryId,
                DirectoryEntryName = entry.Name,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPaidAllTime = totalPaidAllTime,
                Rows = invoices.Select(i =>
                {
                    var days = Math.Max(1, (i.CampaignEndDate.Date - i.CampaignStartDate.Date).TotalDays);
                    var avg = Math.Round((double)(i.Amount / (decimal)days), 2);

                    return new DirectoryManager.Web.Models.Reports.AdvertiserInvoiceRow
                    {
                        SponsoredListingInvoiceId = i.SponsoredListingInvoiceId,
                        Amount = i.Amount,
                        Currency = i.Currency.ToString(),
                        CampaignStartDate = i.CampaignStartDate,
                        CampaignEndDate = i.CampaignEndDate,
                        AvgUsdPerDay = avg,
                        SponsorshipType = i.SponsorshipType.ToString(),
                        PaymentStatus = i.PaymentStatus.ToString(),
                        CreateDate = i.CreateDate,
                    };
                }).ToList()
            };

            return this.View("AdvertiserInvoices", model);
        }
    }
}