// DirectoryManager.Web/Controllers/DirectoryEntryReviewsController.cs
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Controllers
{
    [Route("directory-entry-reviews")]
    public class DirectoryEntryReviewsController : Controller
    {
        private readonly IDirectoryEntryReviewRepository repo;

        public DirectoryEntryReviewsController(IDirectoryEntryReviewRepository repo) => this.repo = repo;

        [HttpGet("")]                                // GET /directory-entry-reviews
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var items = await this.repo.ListAsync(page, pageSize);
            this.ViewBag.Total = await this.repo.CountAsync();
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(items);
        }

        [HttpGet("{id:int}")]                        // GET /directory-entry-reviews/123
        public async Task<IActionResult> Details(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null) return this.NotFound();
            return this.View(item);
        }

        [HttpGet("create")]                          // GET /directory-entry-reviews/create
        public IActionResult Create() => this.View(new DirectoryEntryReview());

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDirectoryEntryReviewInputModel input, CancellationToken ct)
        {
            if (!this.ModelState.IsValid)
            {
                // ModelState contains field errors; the view will render them.
                return this.View(input);
            }

            var entity = new DirectoryEntryReview
            {
                DirectoryEntryId = input.DirectoryEntryId,
                Rating = input.Rating,
                Body = input.Body,
                CreateDate = DateTime.UtcNow
            };

            try
            {
                await this.repo.AddAsync(entity, ct);
                this.TempData["SuccessMessage"] = "Review created.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save the review. Please check your inputs and try again.");
                return this.View(input);
            }
            catch
            {
                this.ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return this.View(input);
            }
        }

        [HttpGet("{id:int}/edit")]                   // GET /directory-entry-reviews/123/edit
        public async Task<IActionResult> Edit(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null) return this.NotFound();
            return this.View(item);
        }

        [HttpGet("{id:int}/edit")]                   // GET /directory-entry-reviews/123/edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DirectoryEntryReview model)
        {
            // replace DirectoryEntryReviewId with your PK if named differently
            var pk = model.DirectoryEntryReviewId;
            if (id != pk) return this.BadRequest();
            if (!this.ModelState.IsValid) return this.View(model);
            await this.repo.UpdateAsync(model);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("{id:int}/delete")]                // POST /directory-entry-reviews/123/delete
        public async Task<IActionResult> Delete(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null) return this.NotFound();
            return this.View(item);
        }

        [HttpPost("{id:int}/delete")]                // POST /directory-entry-reviews/123/delete
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await this.repo.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
