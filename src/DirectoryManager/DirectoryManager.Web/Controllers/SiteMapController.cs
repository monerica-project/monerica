using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Migrations;
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
            var lastFeaturedDate = await this.directoryEntrySelectionRepository.GetMostRecentModifiedDateAsync();
            var lastDirectoryEntryDate = this.directoryEntryRepository.GetLastRevisionDate();
            var lastContentSnippetUpdate = this.contentSnippetRepository.GetLastUpdateDate();
            var lastPaidInvoiceUpdate = this.sponsoredListingInvoiceRepository.GetLastPaidInvoiceUpdateDate();
            var nextAdExpiration = await this.sponsoredListingRepository.GetNextExpirationDateAsync();
            var sponsoredListings = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            var isAdSpaceAvailable = sponsoredListings.Count() < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
            var mostRecentUpdateDate = this.GetLatestUpdateDate(lastDirectoryEntryDate, lastContentSnippetUpdate, lastPaidInvoiceUpdate, nextAdExpiration);

            if (isAdSpaceAvailable)
            {
                // TODO: do this for sub category sponsors
                mostRecentUpdateDate = DateTime.UtcNow;
            }

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
            var allSubcategoryAds = await this.sponsoredListingRepository.GetLastChangeDatesBySubcategoryAsync();
            var allCategoryAds = await this.sponsoredListingRepository.GetLastChangeDatesByCategoryAsync();

            // Iterate through categories and subcategories to build the sitemap
            foreach (var category in categories)
            {
                await this.AddCategoryPages(
                    lastFeaturedDate,
                    mostRecentUpdateDate,
                    siteMapHelper,
                    allCategoriesLastModified,
                    allSubcategoriesLastModified,
                    allSubCategoriesItemsLastModified,
                    allSubcategoryAds,
                    allCategoryAds,
                    category);
            }

            var allActiveEntries = await this.directoryEntryRepository.GetAllEntitiesAndPropertiesAsync();

            foreach (var entry in allActiveEntries.Where(x => x.DirectoryStatus != DirectoryStatus.Removed))
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
                    Priority = 0.7,
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

        private async Task AddCategoryPages(
            DateTime lastFeaturedDate,
            DateTime mostRecentUpdateDate,
            SiteMapHelper siteMapHelper,
            Dictionary<int, DateTime> allCategoriesLastModified,
            Dictionary<int, DateTime> allSubcategoriesLastModified,
            Dictionary<int, DateTime> allSubCategoriesItemsLastModified,
            Dictionary<int, DateTime> allSubCategoryAds,
            Dictionary<int, DateTime> allCategoryAds,
            Data.Models.Category category)
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

            var categories = await this.categoryRepository.GetActiveCategoriesAsync();
            DateTime? mostRecentCategoryDate = categories
            .Select(category => new[]
            {
                allCategoriesLastModified.ContainsKey(category.CategoryId)
                    ? allCategoriesLastModified[category.CategoryId]
                    : DateTime.MinValue,
                allCategoryAds.ContainsKey(category.CategoryId)
                    ? allCategoryAds[category.CategoryId]
                    : DateTime.MinValue,
                allCategoryAds.ContainsKey(category.CategoryId)
                    ? allCategoryAds[category.CategoryId]
                    : DateTime.MinValue
            }.Max()).Max();

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
                Priority = 0.6,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = lastChangeForCategoryOrSubcategory // Use mostRecentUpdateDate
            });

            foreach (var subCategory in subCategories)
            {
                this.AddSubcategories(
                    lastFeaturedDate,
                    mostRecentUpdateDate,
                    siteMapHelper,
                    allSubcategoriesLastModified,
                    allSubCategoriesItemsLastModified,
                    allSubCategoryAds,
                    category,
                    lastChangeToCategory,
                    subCategory);
            }
        }

        private void AddSubcategories(
            DateTime lastFeaturedDate,
            DateTime mostRecentUpdateDate,
            SiteMapHelper siteMapHelper,
            Dictionary<int, DateTime> allSubcategoriesLastModified,
            Dictionary<int, DateTime> allSubCategoriesItemsLastModified,
            Dictionary<int, DateTime> allSubCategoryAds,
            Data.Models.Category category,
            DateTime lastChangeToCategory,
            Data.Models.Subcategory subCategory)
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

            // add time so polling can pick up changes
            lastModified = lastModified.AddHours(1);

            // Add subcategory to sitemap
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format(
                    "{0}/{1}/{2}",
                    WebRequestHelper.GetCurrentDomain(this.HttpContext),
                    category.CategoryKey,
                    subCategory.SubCategoryKey),
                Priority = 0.5,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = lastModified // Use mostRecentUpdateDate
            });
        }

        private void AddNewestPagesList(DateTime date, SiteMapHelper siteMapHelper)
        {
            // TODO: every page needs to be indexed, without a query string
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/newest", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.4,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });
        }

        private void AddPages(DateTime date, SiteMapHelper siteMapHelper)
        {
            var contactHtmlConfig = this.contentSnippetRepository.Get(SiteConfigSetting.ContactHtml);

            if (contactHtmlConfig != null && !string.IsNullOrWhiteSpace(contactHtmlConfig.Content))
            {
                var contactHtmlLastModified = new[] { contactHtmlConfig?.UpdateDate, contactHtmlConfig?.CreateDate, date }
                    .Where(d => d.HasValue)
                    .Max() ?? date;

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format("{0}/contact", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                    Priority = 0.8,
                    ChangeFrequency = ChangeFrequency.Monthly,
                    LastMod = contactHtmlLastModified
                });
            }

            var donationHtmlConfig = this.contentSnippetRepository.Get(SiteConfigSetting.DonationHtml);

            if (donationHtmlConfig != null && !string.IsNullOrWhiteSpace(donationHtmlConfig.Content))
            {
                var donationHtmlLastModified = new[] { donationHtmlConfig?.UpdateDate, donationHtmlConfig?.CreateDate, date }
                    .Where(d => d != null)
                    .Max() ?? date;

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format("{0}/donate", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                    Priority = 0.2,
                    ChangeFrequency = ChangeFrequency.Monthly,
                    LastMod = donationHtmlLastModified
                });
            }

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/sitemap", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/status/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), DirectoryStatus.Verified.ToString().ToLower()),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/status/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), DirectoryStatus.Scam.ToString().ToLower()),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/status/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), DirectoryStatus.Questionable.ToString().ToLower()),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/rss/feed.xml", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.9,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = DateTime.UtcNow
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