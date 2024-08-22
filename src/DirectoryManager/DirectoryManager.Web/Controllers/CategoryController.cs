﻿using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class CategoryController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly IMemoryCache cache;

        public CategoryController(
            UserManager<ApplicationUser> userManager,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.cache = cache;
        }

        [Route("category/index")]
        public async Task<IActionResult> Index()
        {
            var categories = await this.categoryRepository.GetAllAsync();
            return this.View(categories);
        }

        [Route("category/create")]
        [HttpGet]
        public IActionResult Create()
        {
            return this.View();
        }

        [AllowAnonymous]
        [HttpGet("{categoryKey}")]
        public async Task<IActionResult> CategorySubCategories(string categoryKey)
        {
            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);
            if (category == null)
            {
                return this.NotFound();
            }

            var subCategories = await this.subCategoryRepository.GetActiveSubCategoriesAsync(category.CategoryId);
            var subCategoryItems = subCategories.Select(sc => new SubCategoryViewModel
            {
                CategoryKey = categoryKey,
                Name = sc.Name,
                SubCategoryKey = sc.SubCategoryKey,
                Description = sc.Description
            }).ToList();

            var model = new CategoryViewModel()
            {
                PageHeader = category.Name,
                PageTitle = category.Name,
                Description = category.Description,
                MetaDescription = category.MetaDescription,
                Note = category.Note,
                SubCategoryItems = subCategoryItems,
            };

            return this.View("CategorySubCategories", model);
        }

        [Route("category/create")]
        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            category.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            category.Name = category.Name.Trim();
            category.CategoryKey = StringHelpers.UrlKey(category.Name);
            category.Description = category.Description?.Trim();
            category.Note = category.Note?.Trim();
            category.MetaDescription = category.MetaDescription?.Trim();

            await this.categoryRepository.CreateAsync(category);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("category/edit")]
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

        [Route("category/edit")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            var existingCategory = await this.categoryRepository.GetByIdAsync(id);

            if (existingCategory == null)
            {
                return this.NotFound();
            }

            existingCategory.Name = category.Name.Trim();

            if (!string.IsNullOrWhiteSpace(category.CategoryKey))
            {
                existingCategory.CategoryKey = StringHelpers.UrlKey(category.CategoryKey.Trim());
            }
            else
            {
                existingCategory.CategoryKey = StringHelpers.UrlKey(category.Name);
            }

            existingCategory.Description = category.Description?.Trim();
            existingCategory.Note = category.Note?.Trim();
            existingCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingCategory.MetaDescription = category.MetaDescription?.Trim();

            await this.categoryRepository.UpdateAsync(existingCategory);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("category/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.categoryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}