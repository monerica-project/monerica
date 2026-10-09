using DirectoryManager.Data.Constants;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.AffiliateCommissionPaid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("affiliatecommissionearned")]
    public class AffiliateCommissionEarnedController : Controller
    {
        private const int DefaultPageSize = 25;

        private readonly IAffiliateCommissionEarnedRepository commissionRepo;
        private readonly IDirectoryEntryRepository directoryEntryRepo;
        private readonly UserManager<ApplicationUser> userManager;

        public AffiliateCommissionEarnedController(
            IAffiliateCommissionEarnedRepository commissionRepo,
            IDirectoryEntryRepository directoryEntryRepo,
            UserManager<ApplicationUser> userManager)
        {
            this.commissionRepo = commissionRepo;
            this.directoryEntryRepo = directoryEntryRepo;
            this.userManager = userManager;
        }

        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = DefaultPageSize,
            int? directoryEntryId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? quickRange = null)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1 || pageSize > 200)
            {
                pageSize = DefaultPageSize;
            }

            // A quick-range preset (no-JS dropdown) overrides any posted start/end dates and
            // populates them, so the resolved dates also show in the date inputs.
            quickRange = ApplyQuickRange(quickRange, ref startDate, ref endDate);

            IEnumerable<AffiliateCommissionEarned> items;
            int totalCount;
            decimal totalUsd;
            IReadOnlyDictionary<Currency, decimal> currencyTotals;

            if (directoryEntryId.HasValue && directoryEntryId.Value > 0)
            {
                var result = await this.commissionRepo.GetPagedByDirectoryEntryAsync(
                    directoryEntryId.Value, page, pageSize);
                items = result.Items;
                totalCount = result.TotalCount;
                totalUsd = await this.commissionRepo
                    .GetTotalUsdValueByDirectoryEntryAsync(directoryEntryId.Value);
                currencyTotals = await this.commissionRepo
                    .GetCurrencyTotalsByDirectoryEntryAsync(directoryEntryId.Value);
            }
            else if (startDate.HasValue && endDate.HasValue)
            {
                var result = await this.commissionRepo.GetPagedByDateRangeAsync(
                    startDate.Value, endDate.Value, page, pageSize);
                items = result.Items;
                totalCount = result.TotalCount;
                totalUsd = await this.commissionRepo
                    .GetTotalUsdValueByDateRangeAsync(startDate.Value, endDate.Value);
                currencyTotals = await this.commissionRepo
                    .GetCurrencyTotalsByDateRangeAsync(startDate.Value, endDate.Value);
            }
            else
            {
                var result = await this.commissionRepo.GetPagedAsync(page, pageSize);
                items = result.Items;
                totalCount = result.TotalCount;
                totalUsd = await this.commissionRepo.GetTotalUsdValueAsync();
                currencyTotals = await this.commissionRepo.GetCurrencyTotalsAsync();
            }

            var vm = new AffiliateCommissionEarnedListViewModel
            {
                Items = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalUsdValue = totalUsd,
                CurrencyTotals = currencyTotals,
                DirectoryEntryId = directoryEntryId,
                StartDate = startDate,
                EndDate = endDate,
                QuickRange = quickRange,
                DirectoryEntries = await this.BuildDirectoryEntrySelectListAsync(directoryEntryId)
            };

            return this.View(vm);
        }

        /// <summary>
        /// Resolves a quick date-range preset into concrete start/end dates. Returns the
        /// normalized preset key (empty for a custom range). End date is end-of-today so the
        /// current day's commissions are included while the date input still shows today.
        /// </summary>
        private static string ApplyQuickRange(string? quickRange, ref DateTime? startDate, ref DateTime? endDate)
        {
            if (string.IsNullOrWhiteSpace(quickRange))
            {
                return string.Empty;
            }

            var today = DateTime.UtcNow.Date;
            var endOfToday = today.AddDays(1).AddSeconds(-1);

            switch (quickRange.Trim().ToLowerInvariant())
            {
                case "currentyear":
                    startDate = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    endDate = endOfToday;
                    return "currentyear";

                case "last1year":
                    startDate = today.AddYears(-1);
                    endDate = endOfToday;
                    return "last1year";

                default:
                    return string.Empty;
            }
        }

        [HttpGet("totals")]
        public async Task<IActionResult> Totals(DateTime? startDate = null, DateTime? endDate = null)
        {
            IEnumerable<AffiliateCommissionEarnedTotal> totals;
            decimal grandTotalUsd;

            if (startDate.HasValue && endDate.HasValue)
            {
                totals = await this.commissionRepo.GetTotalsByDirectoryEntryAsync(
                    startDate.Value, endDate.Value);
                grandTotalUsd = await this.commissionRepo
                    .GetTotalUsdValueByDateRangeAsync(startDate.Value, endDate.Value);
            }
            else
            {
                totals = await this.commissionRepo.GetTotalsByDirectoryEntryAsync();
                grandTotalUsd = await this.commissionRepo.GetTotalUsdValueAsync();
            }

            var vm = new AffiliateCommissionEarnedTotalsViewModel
            {
                Totals = totals.ToList(),
                StartDate = startDate,
                EndDate = endDate,
                GrandTotalUsd = grandTotalUsd
            };

            return this.View(vm);
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create(int? directoryEntryId = null)
        {
            var vm = new AffiliateCommissionEarnedEditViewModel
            {
                CommissionDate = DateTime.UtcNow,
                DirectoryEntryId = directoryEntryId ?? 0,
                DirectoryEntries = await this.BuildDirectoryEntrySelectListAsync(directoryEntryId),
                CurrencyOptions = this.BuildCurrencyOptions(null)
            };

            return this.View(vm);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AffiliateCommissionEarnedEditViewModel vm)
        {
            if (!this.ModelState.IsValid)
            {
                vm.DirectoryEntries = await this.BuildDirectoryEntrySelectListAsync(vm.DirectoryEntryId);
                vm.CurrencyOptions = this.BuildCurrencyOptions(vm.PaymentCurrency);
                return this.View(vm);
            }

            var userId = this.userManager.GetUserId(this.User) ?? string.Empty;

            var entity = new AffiliateCommissionEarned
            {
                DirectoryEntryId = vm.DirectoryEntryId,
                CommissionDate = vm.CommissionDate,
                UsdValue = vm.UsdValue,
                PaymentCurrency = vm.PaymentCurrency,
                PaymentCurrencyAmount = vm.PaymentCurrencyAmount,
                TransactionId = string.IsNullOrWhiteSpace(vm.TransactionId) ? null : vm.TransactionId.Trim(),
                Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim(),
                CreatedByUserId = userId
            };

            await this.commissionRepo.CreateAsync(entity);

            this.TempData["Message"] = "Commission created.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var existing = await this.commissionRepo.GetByIdAsync(id);
            if (existing == null)
            {
                return this.NotFound();
            }

            var vm = new AffiliateCommissionEarnedEditViewModel
            {
                AffiliateCommissionEarnedId = existing.AffiliateCommissionEarnedId,
                DirectoryEntryId = existing.DirectoryEntryId,
                CommissionDate = existing.CommissionDate,
                UsdValue = existing.UsdValue,
                PaymentCurrency = existing.PaymentCurrency,
                PaymentCurrencyAmount = existing.PaymentCurrencyAmount,
                TransactionId = existing.TransactionId,
                Note = existing.Note,
                DirectoryEntries = await this.BuildDirectoryEntrySelectListAsync(existing.DirectoryEntryId),
                CurrencyOptions = this.BuildCurrencyOptions(existing.PaymentCurrency)
            };

            return this.View(vm);
        }

        [HttpPost("edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AffiliateCommissionEarnedEditViewModel vm)
        {
            if (id != vm.AffiliateCommissionEarnedId)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                vm.DirectoryEntries = await this.BuildDirectoryEntrySelectListAsync(vm.DirectoryEntryId);
                vm.CurrencyOptions = this.BuildCurrencyOptions(vm.PaymentCurrency);
                return this.View(vm);
            }

            var existing = await this.commissionRepo.GetByIdAsync(id);
            if (existing == null)
            {
                return this.NotFound();
            }

            var userId = this.userManager.GetUserId(this.User) ?? string.Empty;

            existing.DirectoryEntryId = vm.DirectoryEntryId;
            existing.CommissionDate = vm.CommissionDate;
            existing.UsdValue = vm.UsdValue;
            existing.PaymentCurrency = vm.PaymentCurrency;
            existing.PaymentCurrencyAmount = vm.PaymentCurrencyAmount;
            existing.TransactionId = string.IsNullOrWhiteSpace(vm.TransactionId) ? null : vm.TransactionId.Trim();
            existing.Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim();
            existing.UpdatedByUserId = userId;

            await this.commissionRepo.UpdateAsync(existing);

            this.TempData["Message"] = "Commission updated.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.commissionRepo.GetByIdAsync(id);
            if (existing == null)
            {
                return this.NotFound();
            }

            return this.View(existing);
        }

        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await this.commissionRepo.DeleteAsync(id);
            this.TempData["Message"] = "Commission deleted.";
            return this.RedirectToAction(nameof(this.Index));
        }

        private async Task<List<SelectListItem>> BuildDirectoryEntrySelectListAsync(int? selectedId)
        {
            var entries = await this.directoryEntryRepo.GetAllAsync();

            return entries
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.DirectoryEntryId.ToString(),
                    Text = e.Name,
                    Selected = selectedId.HasValue && e.DirectoryEntryId == selectedId.Value
                })
                .ToList();
        }

        private List<SelectListItem> BuildCurrencyOptions(Currency? selected)
        {
            return Enum.GetValues<Currency>()
                .Select(c => new SelectListItem
                {
                    Value = ((int)c).ToString(),
                    Text = c.ToString(),
                    Selected = selected.HasValue && c == selected.Value
                })
                .ToList();
        }
    }
}