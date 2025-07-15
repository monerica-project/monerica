using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class StatusController : Controller
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subcategoryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ICacheService cacheService;

        public StatusController(
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subcategoryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ICacheService cacheService)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.categoryRepository = categoryRepository;
            this.subcategoryRepository = subcategoryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.cacheService = cacheService;
        }

        [HttpGet("status/{status}")]
        public async Task<IActionResult> Index(DirectoryStatus status)
        {
            var filteredEntries = await this.directoryEntryRepository.GetActiveEntriesByStatusAsync(status);
            var activeCategories = await this.categoryRepository.GetActiveCategoriesAsync();
            var filteredSubcategories = await this.subcategoryRepository.GetAllAsync();

            var filteredSubcategoriesWithEntries = filteredSubcategories
                .Where(sc => filteredEntries.Any(entry => entry.SubCategoryId == sc.SubcategoryId))
                .ToList();

            var filteredCategoriesWithEntries = activeCategories
                .Where(category => filteredSubcategoriesWithEntries.Any(sc => sc.CategoryId == category.CategoryId))
                .ToList();

            var mainSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            var subCategorySponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);
            var categorySponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);

            this.ViewBag.FilteredEntries = filteredEntries;
            this.ViewBag.FilteredSubcategories = filteredSubcategoriesWithEntries;
            this.ViewBag.FilteredCategories = filteredCategoriesWithEntries;
            this.ViewBag.MainSponsors = mainSponsors;
            this.ViewBag.SubCategorySponsors = subCategorySponsors;
            this.ViewBag.CategorySponsors = categorySponsors;
            this.ViewBag.Link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            this.ViewBag.Link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);
            this.ViewBag.Status = status;

            return this.View();
        }
    }
}