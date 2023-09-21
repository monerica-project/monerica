using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SubCategoryController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;

        public SubCategoryController(
            UserManager<ApplicationUser> userManager,
            ISubCategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            ITrafficLogRepository trafficLogRepository)
                : base(trafficLogRepository)
        {
            this.userManager = userManager;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId = null)
        {
            IEnumerable<SubCategory> subCategories;

            if (categoryId.HasValue)
            {
                subCategories = await this.subCategoryRepository.GetAllAsync();
                subCategories = subCategories.Where(sc => sc.CategoryId == categoryId.Value);
            }
            else
            {
                subCategories = await this.subCategoryRepository.GetAllAsync();
            }

            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync(); // For dropdown list

            return this.View(subCategories);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync();
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(SubCategory subCategory)
        {
            subCategory.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            subCategory.SubCategoryKey = TextHelpers.UrlKey(subCategory.Name);
            subCategory.Name = subCategory.Name.Trim();
            subCategory.Description = subCategory.Description?.Trim();
            subCategory.Note = subCategory.Note?.Trim();

            await this.subCategoryRepository.CreateAsync(subCategory);

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var subCategory = await this.subCategoryRepository.GetByIdAsync(id);
            if (subCategory == null)
            {
                return this.NotFound();
            }

            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync();
            return this.View(subCategory);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SubCategory subCategory)
        {
            var existingSubCategory = await this.subCategoryRepository.GetByIdAsync(subCategory.Id);

            if (existingSubCategory == null)
            {
                return this.NotFound();
            }

            existingSubCategory.Name = subCategory.Name.Trim();
            existingSubCategory.SubCategoryKey = TextHelpers.UrlKey(subCategory.Name.Trim());
            existingSubCategory.CategoryId = subCategory.CategoryId;
            existingSubCategory.Description = subCategory.Description?.Trim();
            existingSubCategory.Note = subCategory.Note?.Trim();
            existingSubCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);

            await this.subCategoryRepository.UpdateAsync(existingSubCategory);
            return this.RedirectToAction(nameof(this.Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            await this.subCategoryRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}