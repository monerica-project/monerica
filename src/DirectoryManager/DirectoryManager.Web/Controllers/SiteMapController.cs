using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class SiteMapController : Controller
    {
        private const int MaxPageSizeForSiteMap = 50000;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache memoryCache;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly IContentSnippetRepository contentSnippetRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IDirectoryEntrySelectionRepository directoryEntrySelectionRepository;

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            IContentSnippetRepository contentSnippetRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            IDirectoryEntrySelectionRepository directoryEntrySelectionRepository)
        {
            this.cacheService = cacheService;
            this.memoryCache = memoryCache;
            this.directoryEntryRepository = directoryEntryRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.contentSnippetRepository = contentSnippetRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.directoryEntrySelectionRepository = directoryEntrySelectionRepository;
        }

        [Route("sitemap_index.xml")]
        public IActionResult SiteMapIndex()
        {
            return this.RedirectPermanent("~/sitemap.xml");
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> IndexAsync()
        {
            var lastFeatured = await this.directoryEntrySelectionRepository.GetEntriesForSelection(EntrySelectionType.Featured);
            var lastFeaturedDate = lastFeatured.Select(x => x.UpdateDate ?? x.CreateDate).DefaultIfEmpty(DateTime.MinValue).Max();
            var lastDirectoryEntryDate = this.directoryEntryRepository.GetLastRevisionDate();
            var lastContentSnippetUpdate = this.contentSnippetRepository.GetLastUpdateDate();
            var lastPaidInvoiceUpdate = this.sponsoredListingInvoiceRepository.GetLastPaidInvoiceUpdateDate();
            var nextAdExpiration = await this.sponsoredListingRepository.GetNextExpirationDate();
            var mostRecentUpdateDate = this.GetLatestUpdateDate(lastDirectoryEntryDate, lastContentSnippetUpdate, lastPaidInvoiceUpdate, nextAdExpiration);

            // Get the last modification date for any sponsored listing
            var lastSponsoredListingChange = await this.sponsoredListingRepository.GetLastChangeDateForMainSponsorAsync();
            mostRecentUpdateDate = lastSponsoredListingChange.HasValue && lastSponsoredListingChange > mostRecentUpdateDate
                ? lastSponsoredListingChange.Value
                : mostRecentUpdateDate;

            var siteMapHelper = new SiteMapHelper();

            // Add the root sitemap item
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = WebRequestHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = mostRecentUpdateDate // Always use mostRecentUpdateDate here
            });

            // Add additional pages to the sitemap
            this.AddNewestPagesList(mostRecentUpdateDate, siteMapHelper); // Passing mostRecentUpdateDate
            this.AddPages(mostRecentUpdateDate, siteMapHelper); // Passing mostRecentUpdateDate

            // Get active categories and their last modified dates
            var categories = await this.categoryRepository.GetActiveCategoriesAsync();
            var allCategoriesLastModified = await this.categoryRepository.GetAllCategoriesLastChangeDatesAsync();

            // Get last modified dates for subcategories and items within subcategories
            var allSubcategoriesLastModified = await this.subCategoryRepository.GetAllSubCategoriesLastChangeDatesAsync();
            var allSubCategoriesItemsLastModified = await this.directoryEntryRepository.GetLastModifiedDatesBySubCategoryAsync();
            var allSubCategoryAds = await this.sponsoredListingRepository.GetLastChangeDatesBySubCategoryAsync();

            // Iterate through categories and subcategories to build the sitemap
            foreach (var category in categories)
            {
                var lastChangeToCategory = allCategoriesLastModified[category.CategoryId];

                // Use mostRecentUpdateDate for categories
                lastChangeToCategory = mostRecentUpdateDate > lastChangeToCategory
                    ? mostRecentUpdateDate
                    : lastChangeToCategory;

                // Get active subcategories for the current category
                var subCategories = await this.subCategoryRepository.GetActiveSubCategoriesAsync(category.CategoryId);

                // Determine the most recent subcategory change date
                DateTime? mostRecentSubcategoryDate = subCategories
                    .Select(subCategory => new[]
                    {
                allSubcategoriesLastModified.ContainsKey(subCategory.SubCategoryId)
                    ? allSubcategoriesLastModified[subCategory.SubCategoryId]
                    : DateTime.MinValue,
                allSubCategoriesItemsLastModified.ContainsKey(subCategory.SubCategoryId)
                    ? allSubCategoriesItemsLastModified[subCategory.SubCategoryId]
                    : DateTime.MinValue,
                allSubCategoryAds.ContainsKey(subCategory.SubCategoryId)
                    ? allSubCategoryAds[subCategory.SubCategoryId]
                    : DateTime.MinValue
                    }.Max())
                    .Max();

                // Compare the most recent subcategory change date with the category change date
                var lastChangeForCategoryOrSubcategory = mostRecentSubcategoryDate.HasValue && mostRecentSubcategoryDate > lastChangeToCategory
                    ? mostRecentSubcategoryDate.Value
                    : lastChangeToCategory;

                // Override with mostRecentUpdateDate if needed
                lastChangeForCategoryOrSubcategory = mostRecentUpdateDate > lastChangeForCategoryOrSubcategory
                    ? mostRecentUpdateDate
                    : lastChangeForCategoryOrSubcategory;

                // Add category to sitemap
                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format("{0}/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), category.CategoryKey),
                    Priority = 1.0,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMod = lastChangeForCategoryOrSubcategory // Use mostRecentUpdateDate
                });

                foreach (var subCategory in subCategories)
                {
                    var lastChangeToSubcategory = allSubcategoriesLastModified.ContainsKey(subCategory.SubCategoryId)
                        ? allSubcategoriesLastModified[subCategory.SubCategoryId]
                        : lastChangeToCategory;

                    var lastChangeToSubcategoryItem = allSubCategoriesItemsLastModified.ContainsKey(subCategory.SubCategoryId)
                        ? allSubCategoriesItemsLastModified[subCategory.SubCategoryId]
                        : lastChangeToSubcategory;

                    var lastChangeToSubcategoryAd = allSubCategoryAds.ContainsKey(subCategory.SubCategoryId)
                        ? allSubCategoryAds[subCategory.SubCategoryId]
                        : lastChangeToSubcategoryItem;

                    // Determine the most recent change date
                    var lastModified = new[]
                    {
                        lastFeaturedDate,
                        lastChangeToSubcategory,
                        lastChangeToSubcategoryItem,
                        lastChangeToSubcategoryAd,
                        mostRecentUpdateDate
                    }.Max();

                    // Add subcategory to sitemap
                    siteMapHelper.SiteMapItems.Add(new SiteMapItem
                    {
                        Url = string.Format(
                            "{0}/{1}/{2}",
                            WebRequestHelper.GetCurrentDomain(this.HttpContext),
                            category.CategoryKey,
                            subCategory.SubCategoryKey),
                        Priority = 1.0,
                        ChangeFrequency = ChangeFrequency.Weekly,
                        LastMod = lastModified // Use mostRecentUpdateDate
                    });
                }
            }

            var allActiveEntries = await this.directoryEntryRepository.GetAllEntitiesAndPropertiesAsync();

            foreach (var entry in allActiveEntries.Where(x => x.DirectoryStatus != Data.Enums.DirectoryStatus.Removed))
            {
                var directoryItemLastMod = new[] { entry.CreateDate, entry.UpdateDate ?? entry.CreateDate, mostRecentUpdateDate }.Max();

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format(
                            "{0}/{1}/{2}/{3}",
                            WebRequestHelper.GetCurrentDomain(this.HttpContext),
                            entry.SubCategory?.Category.CategoryKey,
                            entry.SubCategory?.SubCategoryKey,
                            entry.DirectoryEntryKey),
                    Priority = 1.0,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMod = directoryItemLastMod // Use mostRecentUpdateDate
                });
            }

            // Generate the sitemap XML
            var xml = siteMapHelper.GenerateXml();

            return this.Content(xml, "text/xml");
        }

        [Route("sitemap")]
        public async Task<IActionResult> SiteMap()
        {
            var model = new HtmlSiteMapModel();

            var categories = await this.categoryRepository.GetActiveCategoriesAsync();

            foreach (var category in categories)
            {
                var categoryPages = new SectionPage()
                {
                    AnchorText = category.Name,
                    CanonicalUrl = string.Format("{0}/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), category.CategoryKey),
                };

                var subCategories = await this.subCategoryRepository.GetActiveSubCategoriesAsync(category.CategoryId);

                foreach (var subCategory in subCategories)
                {
                    categoryPages.ChildPages.Add(new SectionPage()
                    {
                        AnchorText = subCategory.Name,
                        CanonicalUrl = string.Format(
                            "{0}/{1}/{2}",
                            WebRequestHelper.GetCurrentDomain(this.HttpContext),
                            category.CategoryKey,
                            subCategory.SubCategoryKey),
                    });
                }

                model.SectionPages.Add(categoryPages);
            }

            var canonicalDomain = this.cacheService.GetSnippet(SiteConfigSetting.CanonicalDomain);

            this.ViewData[Constants.StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "sitemap");

            return this.View("Index", model);
        }

        private void AddNewestPagesList(DateTime date, SiteMapHelper siteMapHelper)
        {
            // TODO: every page needs to be indexed, without a query string
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/newest", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });
        }

        private void AddPages(DateTime date, SiteMapHelper siteMapHelper)
        {
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/contact", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/sitemap", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/rss/feed.xml", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });
        }

        private DateTime GetLatestUpdateDate(DateTime? lastDirectoryEntryDate, DateTime? lastContentSnippetUpdate, DateTime? lastPaidInvoiceUpdate, DateTime? nextAdExpiration)
        {
            DateTime? latestUpdateDate = null;

            // Determine the most recent date among lastDirectoryEntryDate, lastContentSnippetUpdate, and lastPaidInvoiceUpdate
            if (lastDirectoryEntryDate.HasValue)
            {
                latestUpdateDate = lastDirectoryEntryDate;
            }

            if (lastContentSnippetUpdate.HasValue && (!latestUpdateDate.HasValue || lastContentSnippetUpdate > latestUpdateDate))
            {
                latestUpdateDate = lastContentSnippetUpdate;
            }

            if (lastPaidInvoiceUpdate.HasValue && (!latestUpdateDate.HasValue || lastPaidInvoiceUpdate > latestUpdateDate))
            {
                latestUpdateDate = lastPaidInvoiceUpdate;
            }

            // Check if nextAdExpiration is the same day as the latestUpdateDate or if it is the only available date
            if (nextAdExpiration.HasValue)
            {
                if (!latestUpdateDate.HasValue || nextAdExpiration.Value.Date == latestUpdateDate.Value.Date)
                {
                    latestUpdateDate = nextAdExpiration;
                }
            }

            // Return the latest update date or DateTime.MinValue if none of the dates are provided
            return latestUpdateDate ?? DateTime.MinValue;
        }
    }
}