using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
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
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IContentSnippetRepository contentSnippetRepository;
        private readonly IMemoryCache cache;

        public SubCategoryController(
            UserManager<ApplicationUser> userManager,
            ISubcategoryRepository subcategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IContentSnippetRepository contentSnippetRepository,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.subcategoryRepository = subcategoryRepository;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.contentSnippetRepository = contentSnippetRepository;
            this.cache = cache;
        }

        [Route("subcategory/index")]
        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId = null)
        {
            IEnumerable<SubcategoryDto> subCategories;

            if (categoryId.HasValue)
            {
                subCategories = await this.subcategoryRepository.GetAllAsync();
                subCategories = subCategories.Where(sc => sc.CategoryId == categoryId.Value);
            }
            else
            {
                subCategories = await this.subcategoryRepository.GetAllAsync();
            }

            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync(); // For dropdown list

            return this.View(subCategories);
        }

        [Route("subcategory/create")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync();

            // Pass a new Subcategory model to the view
            var model = new Subcategory();
            return this.View(model);
        }

        [Route("subcategory/create")]
        [HttpPost]
        public async Task<IActionResult> Create(Subcategory subCategory)
        {
            subCategory.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            subCategory.SubCategoryKey = StringHelpers.UrlKey(subCategory.Name);
            subCategory.Name = subCategory.Name.Trim();
            subCategory.Description = subCategory.Description?.Trim();
            subCategory.Note = subCategory.Note?.Trim();
            subCategory.MetaDescription = subCategory.MetaDescription?.Trim();
            subCategory.PageDetails = subCategory.PageDetails?.Trim();

            await this.subcategoryRepository.CreateAsync(subCategory);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("subcategory/edit")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var subCategory = await this.subcategoryRepository.GetByIdAsync(id);
            if (subCategory == null)
            {
                return this.NotFound(); // Handle if the subcategory is not found
            }

            this.ViewBag.Categories = await this.categoryRepository.GetAllAsync();

            // Return the view with the subcategory model
            return this.View(subCategory);
        }

        [Route("subcategory/edit")]
        [HttpPost]
        public async Task<IActionResult> Edit(Subcategory subCategory)
        {
            var existingSubCategory = await this.subcategoryRepository.GetByIdAsync(subCategory.SubCategoryId);

            if (existingSubCategory == null)
            {
                return this.NotFound();
            }

            existingSubCategory.Name = subCategory.Name.Trim();
            existingSubCategory.SubCategoryKey = StringHelpers.UrlKey(existingSubCategory.Name);
            existingSubCategory.CategoryId = subCategory.CategoryId;
            existingSubCategory.Description = subCategory.Description?.Trim();
            existingSubCategory.Note = subCategory.Note?.Trim();
            existingSubCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingSubCategory.MetaDescription = subCategory.MetaDescription?.Trim();
            existingSubCategory.PageDetails = subCategory.PageDetails?.Trim();

            await this.subcategoryRepository.UpdateAsync(existingSubCategory);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [AllowAnonymous]
        [HttpGet("{categoryKey}/{subCategoryKey}")]
        public async Task<IActionResult> SubCategoryListings(
            string categoryKey,
            string subCategoryKey,
            int page = 1)
        {
            const int PageSize = 25;

            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);
            if (category == null)
            {
                return this.NotFound();
            }

            var subCategory = await this.subcategoryRepository
                .GetByCategoryIdAndKeyAsync(category.CategoryId, subCategoryKey);
            if (subCategory == null)
            {
                return this.NotFound();
            }

            // fetch paged entries instead of all
            var paged = await this.directoryEntryRepository
                .GetActiveEntriesBySubcategoryPagedAsync(
                    subCategory.SubCategoryId, page, PageSize);

            this.ViewBag.CategoryKey = category.CategoryKey;
            this.ViewBag.SubCategoryKey = subCategory.SubCategoryKey;
            this.ViewBag.CategoryName = category.Name;
            this.ViewBag.SubCategoryName = subCategory.Name;

            var vm = new CategorySubCategoriesViewModel
            {
                PageHeader = FormattingHelper.SubcategoryFormatting(category.Name, subCategory.Name),
                PageTitle = FormattingHelper.SubcategoryFormatting(category.Name, subCategory.Name),
                MetaDescription = subCategory.MetaDescription,
                PageDetails = subCategory.PageDetails,
                Note = subCategory.Note,
                SubCategoryId = subCategory.SubCategoryId,
                CategoryRelativePath = $"/{category.CategoryKey}",
                CategoryName = category.Name,
                SubcategoryName = subCategory.Name,
                SubCategoryKey = subCategory.SubCategoryKey,
                Category = category,
                PagedEntries = paged,
                CurrentPage = page,
                PageSize = PageSize
            };

            this.SetCannonicalUrl();
            return this.View("SubCategoryListings", vm);
        }

        [Route("subcategory/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.subcategoryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        private void SetCannonicalUrl()
        {
            var canonicalDomainSnippet = this.contentSnippetRepository.Get(Data.Enums.SiteConfigSetting.CanonicalDomain);
            var canonicalDomainValue = canonicalDomainSnippet != null ? canonicalDomainSnippet.Content : string.Empty;

            if (!string.IsNullOrWhiteSpace(canonicalDomainValue))
            {
                // Normalize the canonical domain by ensuring it doesn't end with a '/'
                canonicalDomainValue = canonicalDomainValue.TrimEnd('/');

                // Build the canonical URL
                var originalPathAndQuery = $"{this.Request.Path}{this.Request.QueryString}";
                var canonicalUrl = $"{canonicalDomainValue}{originalPathAndQuery}";

                this.ViewData[StringConstants.CanonicalUrl] = UrlHelper.NormalizeUrl(canonicalUrl);
            }
            else
            {
                // Fallback to original URL if no canonical domain is set
                var originalUrl = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.Path}{this.Request.QueryString}";
                this.ViewData[StringConstants.CanonicalUrl] = UrlHelper.NormalizeUrl(originalUrl);
            }
        }
    }
}