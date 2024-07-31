using DirectoryManager.Data.Repositories.Interfaces;
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
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IContentSnippetRepository contentSnippetRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository,
            IContentSnippetRepository contentSnippetRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ISponsoredListingRepository sponsoredListingRepository)
        {
            this.cacheService = cacheService;
            this.memoryCache = memoryCache;
            this.directoryEntryRepository = directoryEntryRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.contentSnippetRepository = contentSnippetRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
        }

        [Route("sitemap_index.xml")]
        public IActionResult SiteMapIndex()
        {
            return this.RedirectPermanent("~/sitemap.xml");
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> IndexAsync()
        {
            var lastDirectoryEntryDate = this.directoryEntryRepository.GetLastRevisionDate();
            var lastContentSnippetUpdate = this.contentSnippetRepository.GetLastUpdateDate();
            var lastPaidInvoiceUpdate = this.sponsoredListingInvoiceRepository.GetLastPaidInvoiceUpdateDate();
            var nextAdExpiration = await this.sponsoredListingRepository.GetNextExpirationDate();
            var mostRecentUpdateDate = this.GetLatestUpdateDate(lastDirectoryEntryDate, lastContentSnippetUpdate, lastPaidInvoiceUpdate, nextAdExpiration);
            var siteMapHelper = new SiteMapHelper();

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = WebRequestHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = mostRecentUpdateDate
            });

            this.AddNewestPagesList(mostRecentUpdateDate, siteMapHelper);
            this.AddSubmitPages(mostRecentUpdateDate, siteMapHelper);

            var categories = await this.categoryRepository.GetActiveCategoriesAsync();

            foreach (var category in categories)
            {
                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format("{0}/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), category.CategoryKey),
                    Priority = 1.0,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMode = mostRecentUpdateDate
                });

                var subCategories = await this.subCategoryRepository.GetActiveSubCategoriesAsync(category.CategoryId);

                foreach (var subCategory in subCategories)
                {
                    siteMapHelper.SiteMapItems.Add(new SiteMapItem
                    {
                        Url = string.Format(
                            "{0}/{1}/{2}",
                            WebRequestHelper.GetCurrentDomain(this.HttpContext),
                            category.CategoryKey,
                            subCategory.SubCategoryKey),
                        Priority = 1.0,
                        ChangeFrequency = ChangeFrequency.Weekly,
                        LastMode = mostRecentUpdateDate
                    });
                }
            }

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
                LastMode = date
            });
        }

        private void AddSubmitPages(DateTime date, SiteMapHelper siteMapHelper)
        {
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/contact", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/sitemap", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
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