using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ICategoryRepository categoryRepository;

        public CategoryController(
            UserManager<ApplicationUser> userManager,
            ICategoryRepository categoryRepository)
        {
            this.userManager = userManager;
            this.categoryRepository = categoryRepository;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await this.categoryRepository.GetAllAsync();
            return this.View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            category.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            category.Name = category.Name.Trim();
            category.CategoryKey = TextHelpers.UrlKey(category.Name);
            category.Description = category.Description?.Trim();
            category.Note = category.Note?.Trim();

            await this.categoryRepository.CreateAsync(category);
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await this.categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return this.NotFound();
            }

            return this.View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Category category)
        {
            var existingCategory = await this.categoryRepository.GetByIdAsync(category.Id);

            if (existingCategory == null)
            {
                return this.NotFound();
            }

            existingCategory.Name = category.Name.Trim();
            existingCategory.CategoryKey = TextHelpers.UrlKey(category.Name);
            existingCategory.Description = category.Description?.Trim();
            existingCategory.Note = category.Note?.Trim();
            existingCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);

            await this.categoryRepository.UpdateAsync(existingCategory);

            return this.RedirectToAction(nameof(this.Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            await this.categoryRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}