using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("raffles")]
    public class RafflesController : BaseController
    {
        private readonly IRaffleRepository raffleRepository;
        private readonly IDirectoryEntryReviewRaffleEntryRepository entryRepository;

        public RafflesController(
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            IRaffleRepository raffleRepository,
            IDirectoryEntryReviewRaffleEntryRepository entryRepository)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.raffleRepository = raffleRepository;
            this.entryRepository = entryRepository;
        }

        // ---------------------------
        // Index
        // ---------------------------
        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var items = await this.raffleRepository.ListWithCountsAsync(page, pageSize, ct);
            this.ViewBag.Total = await this.raffleRepository.CountAsync(ct);
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(items);
        }

        // ---------------------------
        // Details (with recent entries)
        // ---------------------------
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var raffle = await this.raffleRepository.GetByIdAsync(id, ct);
            if (raffle is null)
            {
                return this.NotFound();
            }

            var entries = await this.entryRepository.ListByRaffleAsync(id, page, pageSize, ct);

            this.ViewBag.Entries = entries;
            this.ViewBag.EntryTotal = await this.entryRepository.CountByRaffleAsync(id, ct);
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;

            return this.View(raffle);
        }

        // ---------------------------
        // Create
        // ---------------------------
        [HttpGet("create")]
        public IActionResult Create()
        {
            var now = DateTime.UtcNow;
            var model = new Raffle
            {
                StartDate = now,
                EndDate = now.AddDays(30),
                IsEnabled = true
            };
            return this.View(model);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Raffle input, CancellationToken ct)
        {
            NormalizeForSave(input);

            if (input.EndDate <= input.StartDate)
            {
                this.ModelState.AddModelError(nameof(input.EndDate), "End date must be after the start date.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(input);
            }

            try
            {
                await this.raffleRepository.AddAsync(input, ct);
                this.TempData["SuccessMessage"] = "Raffle created.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save the raffle. A raffle with this name may already exist.");
                return this.View(input);
            }
        }

        // ---------------------------
        // Edit
        // ---------------------------
        [HttpGet("{id:int}/edit")]
        public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
        {
            var raffle = await this.raffleRepository.GetByIdAsync(id, ct);
            if (raffle is null)
            {
                return this.NotFound();
            }

            return this.View(raffle);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Raffle model, CancellationToken ct = default)
        {
            if (id != model.RaffleId)
            {
                return this.BadRequest();
            }

            NormalizeForSave(model);

            if (model.EndDate <= model.StartDate)
            {
                this.ModelState.AddModelError(nameof(model.EndDate), "End date must be after the start date.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            try
            {
                await this.raffleRepository.UpdateAsync(model, ct);
                this.TempData["SuccessMessage"] = "Raffle updated.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save changes. A raffle with this name may already exist.");
                return this.View(model);
            }
        }

        // ---------------------------
        // Edit a single entry (status + payment reference / txid)
        // ---------------------------
        [HttpGet("{raffleId:int}/entries/{entryId:int}/edit")]
        public async Task<IActionResult> EditEntry(int raffleId, int entryId, CancellationToken ct = default)
        {
            var entry = await this.entryRepository.GetByIdAsync(entryId, ct);
            if (entry is null || entry.RaffleId != raffleId)
            {
                return this.NotFound();
            }

            var model = new RaffleEntryEditInputModel
            {
                DirectoryEntryReviewRaffleEntryId = entry.DirectoryEntryReviewRaffleEntryId,
                RaffleId = raffleId,
                DirectoryEntryReviewId = entry.DirectoryEntryReviewId,
                CreateDate = entry.CreateDate,
                CryptoType = entry.CryptoType,
                CryptoAddress = entry.CryptoAddress,
                Status = entry.Status,
                PaymentReference = entry.PaymentReference,
            };

            return this.View(model);
        }

        [HttpPost("{raffleId:int}/entries/{entryId:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntry(int raffleId, int entryId, RaffleEntryEditInputModel model, CancellationToken ct = default)
        {
            if (entryId != model.DirectoryEntryReviewRaffleEntryId || raffleId != model.RaffleId)
            {
                return this.BadRequest();
            }

            model.CryptoType = (model.CryptoType ?? string.Empty).Trim();
            model.CryptoAddress = (model.CryptoAddress ?? string.Empty).Trim();
            model.PaymentReference = string.IsNullOrWhiteSpace(model.PaymentReference)
                ? null
                : model.PaymentReference.Trim();

            // A payout reference (txid) is required whenever an entry is marked Paid.
            if (model.Status == RaffleEntryStatus.Paid && string.IsNullOrWhiteSpace(model.PaymentReference))
            {
                this.ModelState.AddModelError(
                    nameof(model.PaymentReference),
                    "A payment reference (txid) is required when marking an entry as Paid.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            // Load the tracked entity and mutate it so the existing RowVersion is used
            // for the concurrency check.
            var entry = await this.entryRepository.GetByIdAsync(entryId, ct);
            if (entry is null || entry.RaffleId != raffleId)
            {
                return this.NotFound();
            }

            entry.CryptoType = model.CryptoType;
            entry.CryptoAddress = model.CryptoAddress;
            entry.Status = model.Status;
            entry.PaymentReference = model.PaymentReference;

            try
            {
                await this.entryRepository.UpdateAsync(entry, ct);
                this.TempData["SuccessMessage"] = $"Entry #{entryId} updated.";
                return this.RedirectToAction(nameof(this.Details), new { id = raffleId });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    "This entry was modified by another process. Reload the page and try again.");
                return this.View(model);
            }
        }

        // ---------------------------
        // Delete
        // ---------------------------
        [HttpGet("{id:int}/delete")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            var raffle = await this.raffleRepository.GetByIdAsync(id, ct);
            if (raffle is null)
            {
                return this.NotFound();
            }

            this.ViewBag.EntryTotal = await this.entryRepository.CountByRaffleAsync(id, ct);
            return this.View(raffle);
        }

        [HttpPost("{id:int}/delete/confirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            var entryCount = await this.entryRepository.CountByRaffleAsync(id, ct);
            if (entryCount > 0)
            {
                this.TempData["ErrorMessage"] =
                    $"Cannot delete — this raffle has {entryCount} associated entries. " +
                    "Reassign or delete them first, or disable the raffle instead.";
                return this.RedirectToAction(nameof(this.Details), new { id });
            }

            await this.raffleRepository.DeleteAsync(id, ct);
            this.TempData["SuccessMessage"] = "Raffle deleted.";
            return this.RedirectToAction(nameof(this.Index));
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private static void NormalizeForSave(Raffle r)
        {
            r.Name = (r.Name ?? string.Empty).Trim();
            r.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();

            // Dates coming from <input type="datetime-local"> arrive as Unspecified.
            // Treat them as UTC so they round-trip cleanly.
            if (r.StartDate.Kind == DateTimeKind.Unspecified)
            {
                r.StartDate = DateTime.SpecifyKind(r.StartDate, DateTimeKind.Utc);
            }

            if (r.EndDate.Kind == DateTimeKind.Unspecified)
            {
                r.EndDate = DateTime.SpecifyKind(r.EndDate, DateTimeKind.Utc);
            }
        }
    }
}
