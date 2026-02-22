using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("review-tags")]
    public class ReviewTagsController : Controller
    {
        private readonly IReviewTagRepository repo;

        public ReviewTagsController(IReviewTagRepository repo)
        {
            this.repo = repo;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var tags = await this.repo.ListAllAsync(ct);
            return this.View(tags);
        }

        [HttpGet("create")]
        public IActionResult Create() => this.View(new ReviewTag());

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewTag model, CancellationToken ct)
        {
            NormalizeTag(model);

            // Re-validate with normalized values (Slug now filled)
            this.ModelState.Clear();
            this.TryValidateModel(model);

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            try
            {
                await this.repo.AddAsync(model, ct);
                this.TempData["SuccessMessage"] = "Tag created.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save. That slug may already exist.");
                return this.View(model);
            }
        }

        [HttpGet("{id:int}/edit")]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var tag = await this.repo.GetByIdAsync(id, ct);
            if (tag is null) return this.NotFound();
            return this.View(tag);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReviewTag model, CancellationToken ct)
        {
            if (id != model.ReviewTagId) return this.BadRequest();

            NormalizeTag(model);

            // Re-validate with normalized values (Slug now filled)
            this.ModelState.Clear();
            this.TryValidateModel(model);

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            try
            {
                await this.repo.UpdateAsync(model, ct);
                this.TempData["SuccessMessage"] = "Tag updated.";
                return this.RedirectToAction(nameof(this.Index));
            }
            catch (DbUpdateException)
            {
                this.ModelState.AddModelError(string.Empty, "Could not save. That slug may already exist.");
                return this.View(model);
            }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            await this.repo.DeleteAsync(id, ct);
            this.TempData["SuccessMessage"] = "Tag deleted.";
            return this.RedirectToAction(nameof(this.Index));
        }

        private static void NormalizeTag(ReviewTag model)
        {
            model.Name = (model.Name ?? string.Empty).Trim();

            // ✅ If slug blank, derive from name BEFORE validation
            var rawSlug = string.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug;
            model.Slug = Slugify(rawSlug);

            // Normalize description too (optional)
            if (!string.IsNullOrWhiteSpace(model.Description))
                model.Description = model.Description.Trim();
        }

        private static string Slugify(string s)
        {
            s = (s ?? string.Empty).Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\s+", "-");
            s = Regex.Replace(s, @"[^a-z0-9\-]", "");
            s = s.Trim('-');
            return string.IsNullOrWhiteSpace(s) ? "tag" : s;
        }
    }
}
