using DirectoryManager.Data.Models;
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
    public class SubCategoryController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IMemoryCache cache;

        public SubCategoryController(
            UserManager<ApplicationUser> userManager,
            ISubCategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cache = cache;
        }

        [Route("subcategory/index")]
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

        [Route("subcategory/create")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync();
            return this.View();
        }

        [Route("subcategory/create")]
        [HttpPost]
        public async Task<IActionResult> Create(SubCategory subCategory)
        {
            subCategory.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            subCategory.SubCategoryKey = StringHelpers.UrlKey(subCategory.Name);
            subCategory.Name = subCategory.Name.Trim();
            subCategory.Description = subCategory.Description?.Trim();
            subCategory.Note = subCategory.Note?.Trim();
            subCategory.MetaDescription = subCategory.MetaDescription?.Trim();

            await this.subCategoryRepository.CreateAsync(subCategory);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("subcategory/edit")]
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

        [Route("subcategory/edit")]
        [HttpPost]
        public async Task<IActionResult> Edit(SubCategory subCategory)
        {
            var existingSubCategory = await this.subCategoryRepository.GetByIdAsync(subCategory.SubCategoryId);

            if (existingSubCategory == null)
            {
                return this.NotFound();
            }

            existingSubCategory.Name = subCategory.Name.Trim();

            if (!string.IsNullOrWhiteSpace(subCategory.SubCategoryKey))
            {
                existingSubCategory.SubCategoryKey = StringHelpers.UrlKey(subCategory.SubCategoryKey.Trim());
            }
            else
            {
                existingSubCategory.SubCategoryKey = StringHelpers.UrlKey(existingSubCategory.Name);
            }

            existingSubCategory.CategoryId = subCategory.CategoryId;
            existingSubCategory.Description = subCategory.Description?.Trim();
            existingSubCategory.Note = subCategory.Note?.Trim();
            existingSubCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingSubCategory.MetaDescription = subCategory.MetaDescription?.Trim();

            await this.subCategoryRepository.UpdateAsync(existingSubCategory);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [AllowAnonymous]
        [HttpGet("{categorykey}/{subcategorykey}")]
        public async Task<IActionResult> SubCategoryListings(string categoryKey, string subCategoryKey)
        {
            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);

            if (category == null)
            {
                return this.NotFound();
            }

            var subCategory = await this.subCategoryRepository.GetByCategoryIdAndKeyAsync(category.CategoryId, subCategoryKey);

            if (subCategory == null)
            {
                return this.NotFound();
            }

            var entries = await this.directoryEntryRepository.GetActiveEntriesByCategoryAsync(subCategory.SubCategoryId);

            var model = new CategorySubCategoriesViewModel
            {
                PageHeader = $"{category.Name} > {subCategory.Name}",
                PageTitle = $"{category.Name} > {subCategory.Name}",
                MetaDescription = subCategory.MetaDescription,
                Description = subCategory.Description,
                Note = subCategory.Note,
                SubCategoryId = subCategory.SubCategoryId,
                DirectoryEntries = entries,
                CategoryRelativePath = string.Format("/{0}", category.CategoryKey),
                CategoryName = category.Name
            };

            return this.View("SubCategoryListings", model);
        }

        [Route("subcategory/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.subCategoryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}