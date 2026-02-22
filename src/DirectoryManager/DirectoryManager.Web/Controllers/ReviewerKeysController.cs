using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Route("reviewer-keys")]
    public class ReviewerKeysController : Controller
    {
        private readonly IReviewerKeyRepository repo;

        public ReviewerKeysController(IReviewerKeyRepository repo) => this.repo = repo;

        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var items = await this.repo.ListAsync(page, pageSize);
            var total = await this.repo.CountAsync();
            this.ViewBag.Total = total;
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(items);
        }

        [HttpGet("{id:int}")] // GET /reviewer-keys/123
        public async Task<IActionResult> Details(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpGet("create")]
        public IActionResult Create() => this.View(new ReviewerKey());

        [HttpPost("create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewerKey model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            await this.repo.AddAsync(model);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("{id:int}/edit")] // GET /reviewer-keys/123/edit
        public async Task<IActionResult> Edit(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpPost("{id:int}/edit")] // POST /reviewer-keys/123/edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReviewerKey model)
        {
            if (id != model.ReviewerKeyId)
            {
                return this.BadRequest();
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            await this.repo.UpdateAsync(model);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("{id:int}/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await this.repo.GetByIdAsync(id);
            if (item is null)
            {
                return this.NotFound();
            }

            return this.View(item);
        }

        [HttpPost("{id:int}/delete")] // POST /reviewer-keys/123/delete
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await this.repo.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}