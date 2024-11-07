using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SponsoredListingInvoiceController : Controller
    {
        private readonly ISponsoredListingInvoiceRepository invoiceRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICacheService cacheService;

        public SponsoredListingInvoiceController(
            ISponsoredListingInvoiceRepository invoiceRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISubcategoryRepository subCategoryRepository,
            ICacheService cacheService)
        {
            this.invoiceRepository = invoiceRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.cacheService = cacheService;
        }

        [Route("sponsoredlistinginvoice")]
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var (invoices, totalItems) = await this.invoiceRepository.GetPageAsync(page, pageSize);

            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalItems = totalItems;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.IsPaidOnly = false; // For all invoices

            return this.View(invoices);
        }

        [Route("sponsoredlistinginvoice/paid")]
        [HttpGet]
        public async Task<IActionResult> PaidIndex(int page = 1, int pageSize = 10)
        {
            var (invoices, totalItems) = await this.invoiceRepository.GetPageByTypeAsync(page, pageSize, PaymentStatus.Paid);

            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalItems = totalItems;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.IsPaidOnly = true; // For paid invoices only

            return this.View("Index", invoices); // Reuse the same view
        }

        [Route("sponsoredlistinginvoice/details")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return this.NotFound();
            }

            var sponsoredListingInvoice = await this.invoiceRepository.GetByIdAsync(id.Value);
            if (sponsoredListingInvoice == null)
            {
                return this.NotFound();
            }

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(sponsoredListingInvoice.DirectoryEntryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            Subcategory? subcategory = null;

            if (sponsoredListingInvoice.SubCategoryId != null)
            {
                subcategory = (await this.subCategoryRepository
                                             .GetAllActiveSubCategoriesAsync())
                                             .OrderBy(sc => sc.Category.Name)
                                             .ThenBy(sc => sc.Name)
                                             .Where(sc => sc.SubCategoryId == sponsoredListingInvoice.SubCategoryId.Value)
                                             .FirstOrDefault();
            }

            if (subcategory != null)
            {
                this.ViewBag.SubCategory = subcategory;
            }
            else
            {
                // Handle the case when no matching subcategory is found
                this.ViewBag.SubCategory = null; // or provide a default value if appropriate
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
            {
                CreateDate = directoryEntry.CreateDate,
                UpdateDate = directoryEntry.UpdateDate,
                DateOption = Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = directoryEntry.Link,
                Name = directoryEntry.Name,
                DirectoryEntryKey = directoryEntry.DirectoryEntryKey,
                Contact = directoryEntry.Contact,
                Description = directoryEntry.Description,
                DirectoryEntryId = directoryEntry.DirectoryEntryId,
                DirectoryStatus = directoryEntry.DirectoryStatus,
                Link2 = directoryEntry.Link2,
                Link3 = directoryEntry.Link3,
                Location = directoryEntry.Location,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId
            };

            return this.View(sponsoredListingInvoice);
        }

        [Route("sponsoredlistinginvoice/report")]
        [HttpGet]
        public async Task<IActionResult> Report(DateTime? startDate, DateTime? endDate)
        {
            var now = DateTime.UtcNow;
            var modelStartDate = startDate?.Date ?? now.AddDays(-30).Date;
            var modelEndDate = endDate?.Date ?? now.Date;
            var startOfDayUtc = new DateTime(modelStartDate.Year, modelStartDate.Month, modelStartDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var endOfDayUtc = new DateTime(modelEndDate.Year, modelEndDate.Month, modelEndDate.Day, 23, 59, 59, DateTimeKind.Utc);

            var model = new InvoiceQueryViewModel
            {
                StartDate = startOfDayUtc,
                EndDate = endOfDayUtc
            };

            var result = await this.invoiceRepository.GetTotalsPaidAsync(model.StartDate, model.EndDate);
            model.TotalPaidAmount = result.TotalReceivedAmount;
            model.Currency = result.Currency;
            model.TotalAmount = result.TotalAmount;
            model.PaidInCurrency = result.PaidInCurrency;

            return this.View(model);
        }

        [HttpGet("sponsoredlistinginvoice/monthlyincomebarchart")]
        public async Task<IActionResult> WeeklyPlotImageAsync()
        {
            InvoicePlotting plottingChart = new InvoicePlotting();

            var invoices = await this.invoiceRepository.GetAllAsync();

            var imageBytes = plottingChart.CreateMonthlyIncomeBarChart(invoices.ToList());
            return this.File(imageBytes, StringConstants.PngImage);
        }
    }
}