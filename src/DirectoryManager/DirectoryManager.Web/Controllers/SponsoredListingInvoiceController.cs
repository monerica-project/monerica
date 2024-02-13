using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
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
        private readonly ICacheService cacheService;

        public SponsoredListingInvoiceController(
            ISponsoredListingInvoiceRepository invoiceRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService)
        {
            this.invoiceRepository = invoiceRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheService = cacheService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var (invoices, totalItems) = await this.invoiceRepository.GetPageAsync(page, pageSize);

            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalItems = totalItems;
            this.ViewBag.TotalPages = totalPages;

            return this.View(invoices);
        }

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

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
            {
                DirectoryEntry = directoryEntry,
                Link2Name = link2Name,
                Link3Name = link3Name
            };

            return this.View(sponsoredListingInvoice);
        }


        [HttpGet]
        public async Task<IActionResult> Report(DateTime? startDate, DateTime? endDate)
        {
            var now = DateTime.UtcNow;
            var modelStartDate = startDate ?? now.AddDays(-30);
            var modelEndDate = endDate ?? now;

            var model = new InvoiceQueryViewModel
            {
                StartDate = modelStartDate,
                EndDate = modelEndDate
            };

            var result = await this.invoiceRepository.GetTotalsPaidAsync(modelStartDate, modelEndDate);
            model.TotalPaidAmount = result.TotalReceivedAmount;
            model.Currency = result.Currency;
            model.TotalAmount = result.TotalAmount;
            model.PaidInCurrency = result.PaidInCurrency;

            return this.View(model);
        }
    }
}