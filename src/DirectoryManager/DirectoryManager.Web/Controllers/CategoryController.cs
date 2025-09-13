using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
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
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;

        public CategoryController(
            UserManager<ApplicationUser> userManager,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ICacheService cacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.cacheService = cacheService;
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
        [HttpGet("{categoryKey}/page/{page:int}")]
        public async Task<IActionResult> Category(string categoryKey, int page = 1)
        {
            const int PageSize = IntegerConstants.MediumPageSize;

            // 1) find category
            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);
            if (category == null)
            {
                return this.NotFound();
            }

            // 2) canonical URL
            var cd = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[Constants.StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(cd, categoryKey);

            // 3) fetch paged entries
            var paged = await this.directoryEntryRepository
                .ListEntriesByCategoryAsync(category.CategoryId, page, PageSize);

            // 4) convert toaw view‑models
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var vmItems = ViewModelConverter.ConvertToViewModels(
                paged.Items.ToList(),
                DateDisplayOption.NotDisplayed,
                ItemDisplayType.Normal,
                link2Name,
                link3Name);

            // 5) pull _all_ sponsors and collect their entry‑IDs:
            var mainSponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            var categorySponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);
            var subCatSponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);

            var mainIds = mainSponsors
                .Where(s => s.DirectoryEntry != null)
                .Select(s => s.DirectoryEntry.DirectoryEntryId);

            var categoryIds = categorySponsors
                .Where(s =>
                    s.DirectoryEntry?.SubCategory?.CategoryId == category.CategoryId)
                .Select(s => s.DirectoryEntry.DirectoryEntryId);

            var subCatIds = subCatSponsors
                .Where(s =>
                    s.DirectoryEntry?.SubCategory?.CategoryId == category.CategoryId)
                .Select(s => s.DirectoryEntry.DirectoryEntryId);

            var sponsoredDirectoryEntryIds = mainIds
                .Concat(categoryIds)
                .Concat(subCatIds)
                .Distinct()
                .ToHashSet();

            // 6) build CategoryEntriesViewModel
            var vm = new CategoryEntriesViewModel
            {
                CategoryId = category.CategoryId,
                CategoryKey = category.CategoryKey,
                CategoryName = category.Name,
                Description = category.Description,
                Note = category.Note,
                MetaDescription = category.MetaDescription,

                // <-- now includes main, category & subcategory sponsors:
                SponsoredDirectoryEntryIds = sponsoredDirectoryEntryIds,

                PagedEntries = new PagedResult<DirectoryEntryViewModel>
                {
                    TotalCount = paged.TotalCount,
                    Items = vmItems
                },

                CurrentPage = page,
                PageSize = PageSize
            };

            return this.View("CategoryItems", vm);
        }

        [Route("category/create")]
        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            category.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            this.PrepareCategory(category);

            await this.categoryRepository.CreateAsync(category);
            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
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

            this.UpdateExistingCategory(existingCategory, category);
            existingCategory.UpdatedByUserId = this.userManager.GetUserId(this.User);

            await this.categoryRepository.UpdateAsync(existingCategory);
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

        [Route("category/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.categoryRepository.DeleteAsync(id);

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        private void PrepareCategory(Category category)
        {
            category.Name = category.Name.Trim();
            category.CategoryKey = StringHelpers.UrlKey(category.Name);
            category.Description = category.Description?.Trim();
            category.Note = category.Note?.Trim();
            category.MetaDescription = category.MetaDescription?.Trim();
        }

        private void UpdateExistingCategory(Category existingCategory, Category newCategory)
        {
            existingCategory.Name = newCategory.Name.Trim();
            existingCategory.CategoryKey = StringHelpers.UrlKey(newCategory.Name);
            existingCategory.Description = newCategory.Description?.Trim();
            existingCategory.Note = newCategory.Note?.Trim();
            existingCategory.MetaDescription = newCategory.MetaDescription?.Trim();
        }
    }
}