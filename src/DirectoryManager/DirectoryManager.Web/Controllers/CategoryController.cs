using DirectoryManager.Web.Extensions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
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
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;
        private readonly IDirectoryEntryReviewRepository entryReviewsRepo;

        public CategoryController(
            UserManager<ApplicationUser> userManager,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ICacheService cacheService,
            IMemoryCache cache,
            IDirectoryEntryReviewRepository entryReviewsRepo)
            : base(cache)
        {
            this.userManager = userManager;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.cacheService = cacheService;
            this.cache = cache;
            this.entryReviewsRepo = entryReviewsRepo;
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
            if (category == null || !category.IsEnabled) // retired categories 404
            {
                return this.NotFound();
            }

            // Out-of-range pagination pages 301 to page 1 (the site turns 404s into a soft-404
            // via UseStatusCodePagesWithRedirects, so a permanent redirect is the clean signal).
            // Reject page < 1 BEFORE querying (a negative OFFSET is a 500 on Postgres).
            if (page < 1)
            {
                return this.RedirectPermanent($"/{category.CategoryKey}");
            }

            // 2) canonical URL — the page's own URL (page 1 has no /page/N suffix)
            var cd = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.PageMeta().CanonicalUrl = page > 1
                ? UrlBuilder.CombineUrl(cd, $"{category.CategoryKey}/page/{page}")
                : UrlBuilder.CombineUrl(cd, category.CategoryKey);

            // 3) fetch paged entries
            var paged = await this.directoryEntryRepository
                .ListEntriesByCategoryAsync(category.CategoryId, page, PageSize);

            var totalPages = PagingHelper.TotalPages(paged.TotalCount, PageSize);
            if (page > totalPages)
            {
                return this.RedirectPermanent($"/{category.CategoryKey}");
            }

            // 4) convert to view-models
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var vmItems = ViewModelConverter.ConvertToViewModels(
                paged.Items.ToList(),
                DateDisplayOption.NotDisplayed,
                ItemDisplayType.Normal,
                link2Name,
                link3Name);

            // ✅ ratings for items on this page
            await this.ApplyRatingsAsync(vmItems);

            // 5) pull sponsors and collect their entry IDs (null-safe)
            var mainSponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            var categorySponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);
            var subCatSponsors = await this.sponsoredListingRepository
                .GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);

            var mainIds = mainSponsors
                .Where(s => s.DirectoryEntry != null)
                .Select(s => s.DirectoryEntry!.DirectoryEntryId);

            var categoryIds = categorySponsors
                .Where(s => s.DirectoryEntry != null &&
                            s.DirectoryEntry!.SubCategory?.CategoryId == category.CategoryId)
                .Select(s => s.DirectoryEntry!.DirectoryEntryId);

            // A subcategory sponsorship only highlights the row for the subcategory it was bought
            // for (it may be one of the listing's additional subcategories), not the listing's
            // other rows on this page.
            var sponsoredSubcategoryRows = subCatSponsors
                .Where(s => s.DirectoryEntry != null)
                .Select(s => (s.DirectoryEntryId, s.SubCategoryId ?? s.DirectoryEntry!.SubCategoryId))
                .ToHashSet();

            var sponsoredDirectoryEntryIds = mainIds
                .Concat(categoryIds)
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

                SponsoredDirectoryEntryIds = sponsoredDirectoryEntryIds,
                SponsoredSubcategoryRows = sponsoredSubcategoryRows,

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

            // CategoryKey is unique: pre-check so a duplicate is a validation error, not a 500.
            if (await this.categoryRepository.KeyExistsAsync(category.CategoryKey))
            {
                this.ModelState.AddModelError(string.Empty, DuplicateKeyMessage(category.CategoryKey));
                return this.View(category);
            }

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

            var newKey = StringHelpers.UrlKey(category.Name);
            if (await this.categoryRepository.KeyExistsAsync(newKey, id))
            {
                this.ModelState.AddModelError(string.Empty, DuplicateKeyMessage(newKey));
                await this.SetFeaturableEntriesAsync(id);
                return this.View(category);
            }

            this.UpdateExistingCategory(existingCategory, category);

            // Featured pick must be an Admitted/Verified listing in this category (else cleared).
            var featurable = await this.directoryEntryRepository.GetFeaturableForCategoryAsync(id);
            existingCategory.FeaturedDirectoryEntryId =
                category.FeaturedDirectoryEntryId.HasValue && featurable.Any(o => o.Id == category.FeaturedDirectoryEntryId.Value)
                    ? category.FeaturedDirectoryEntryId
                    : null;
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

            await this.SetFeaturableEntriesAsync(id);
            return this.View(category);
        }

        // NOTE: there is intentionally no hard-delete endpoint. Deleting a category either failed
        // (FK) or cascaded into entries/invoices/sponsorships/reviews; disable it instead.
        private static string DuplicateKeyMessage(string key) =>
            $"Another category already uses the URL key \"{key}\". Choose a different name.";

        private async Task SetFeaturableEntriesAsync(int categoryId)
        {
            this.ViewBag.FeaturableEntries = await this.directoryEntryRepository.GetFeaturableForCategoryAsync(categoryId);
            this.ViewBag.FeaturedScope = "category";
        }

        private void PrepareCategory(Category category)
        {
            category.Name = category.Name.Trim();
            category.CategoryKey = StringHelpers.UrlKey(category.Name);
            category.Description = category.Description?.Trim();
            category.Note = category.Note?.Trim();
            category.MetaDescription = category.MetaDescription?.Trim();
            category.IsEnabled = category.IsEnabled;
        }

        private void UpdateExistingCategory(Category existingCategory, Category newCategory)
        {
            existingCategory.Name = newCategory.Name.Trim();
            existingCategory.CategoryKey = StringHelpers.UrlKey(newCategory.Name);
            existingCategory.Description = newCategory.Description?.Trim();
            existingCategory.Note = newCategory.Note?.Trim();
            existingCategory.MetaDescription = newCategory.MetaDescription?.Trim();
            existingCategory.IsEnabled = newCategory.IsEnabled;
        }

        private async Task ApplyRatingsAsync(List<DirectoryEntryViewModel> items)
        {
            var ids = items.Select(x => x.DirectoryEntryId).Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var ratingMap = await this.entryReviewsRepo.GetRatingSummariesAsync(ids);

            foreach (var item in items)
            {
                if (ratingMap.TryGetValue(item.DirectoryEntryId, out var rs) && rs.ReviewCount > 0)
                {
                    item.AverageRating = rs.AvgRating;
                    item.ReviewCount = rs.ReviewCount;
                }
                else
                {
                    item.AverageRating = null;
                    item.ReviewCount = 0;
                }
            }
        }
    }
}