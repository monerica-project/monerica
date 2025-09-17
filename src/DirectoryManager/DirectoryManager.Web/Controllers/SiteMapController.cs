using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly ITagRepository tagRepository;
        private readonly IDirectoryEntryTagRepository entryTagRepository;
        private readonly IDirectoryEntryReviewRepository directoryEntryReviewRepository;

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            IContentSnippetRepository contentSnippetRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            IDirectoryEntrySelectionRepository directoryEntrySelectionRepository,
            ITagRepository tagRepository,
            IDirectoryEntryTagRepository entryTagRepository,
            IDirectoryEntryReviewRepository directoryEntryReviewRepository)
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
            this.tagRepository = tagRepository;
            this.entryTagRepository = entryTagRepository;
            this.directoryEntryReviewRepository = directoryEntryReviewRepository;
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
            var lastSponsorExpiration = await this.sponsoredListingRepository.GetLastSponsorExpirationDateAsync();
            var mostRecentUpdateDate = this.GetLatestUpdateDate(
                                                lastDirectoryEntryDate,
                                                lastContentSnippetUpdate,
                                                lastPaidInvoiceUpdate,
                                                nextAdExpiration,
                                                lastSponsorExpiration,
                                                isAdSpaceAvailable);

            // Get the last modification date for any sponsored listing
            var lastSponsoredListingChange = await this.sponsoredListingRepository.GetLastChangeDateForMainSponsorAsync();
            var latestApprovedReviewByEntry =
                await this.directoryEntryReviewRepository.GetLatestApprovedReviewDatesByEntryAsync();

            if (latestApprovedReviewByEntry != null && latestApprovedReviewByEntry.Count > 0)
            {
                var latestApprovedReviewDate = latestApprovedReviewByEntry.Values.Max();
                if (latestApprovedReviewDate > mostRecentUpdateDate)
                {
                    mostRecentUpdateDate = latestApprovedReviewDate;
                }
            }

            mostRecentUpdateDate = lastSponsoredListingChange.HasValue && lastSponsoredListingChange > mostRecentUpdateDate
                ? lastSponsoredListingChange.Value
                : mostRecentUpdateDate;

            var siteMapHelper = new SiteMapHelper();
            var domain = WebRequestHelper.GetCurrentDomain(this.HttpContext).TrimEnd('/');

            await this.AddTags(mostRecentUpdateDate, siteMapHelper, domain);

            // Root
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = WebRequestHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = mostRecentUpdateDate
            });

            await this.AddNewestPagesListAsync(mostRecentUpdateDate, siteMapHelper);
            await this.AddPagesAsync(mostRecentUpdateDate, siteMapHelper);
            await this.AddAllTagsPaginationAsync(mostRecentUpdateDate, siteMapHelper);

            var categories = await this.categoryRepository.GetActiveCategoriesAsync();
            var subcategoryEntryCounts = await this.directoryEntryRepository.GetSubcategoryEntryCountsAsync();

            await this.AddCategoryPaginationAsync(mostRecentUpdateDate, siteMapHelper, categories, subcategoryEntryCounts, domain);

            var allCategoriesLastModified = await this.categoryRepository.GetAllCategoriesLastChangeDatesAsync();
            var allSubcategoriesLastModified = await this.subCategoryRepository.GetAllSubCategoriesLastChangeDatesAsync();
            var allSubCategoriesItemsLastModified = await this.directoryEntryRepository.GetLastModifiedDatesBySubCategoryAsync();
            var allSubcategoryAds = await this.sponsoredListingRepository.GetLastChangeDatesBySubcategoryAsync();
            var allCategoryAds = await this.sponsoredListingRepository.GetLastChangeDatesByCategoryAsync();

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
                    subcategoryEntryCounts,
                    domain,
                    category);
            }

            var allActiveEntries = await this.directoryEntryRepository.GetAllEntitiesAndPropertiesAsync();

            foreach (var entry in allActiveEntries.Where(x => x.DirectoryStatus != DirectoryStatus.Removed))
            {
                var baseLastMod = new[]
                {
                    entry.CreateDate,
                    entry.UpdateDate ?? entry.CreateDate,
                    mostRecentUpdateDate
                }.Max();

                var reviewLastMod = latestApprovedReviewByEntry.TryGetValue(entry.DirectoryEntryId, out var rdt)
                    ? rdt
                    : DateTime.MinValue;

                var directoryItemLastMod = new[] { baseLastMod, reviewLastMod }.Max();

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = $"{domain}/{entry.SubCategory?.Category.CategoryKey}/{entry.SubCategory?.SubCategoryKey}/{entry.DirectoryEntryKey}",
                    Priority = 0.7,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMod = directoryItemLastMod
                });
            }

            foreach (var entry in allActiveEntries.Where(x => x.DirectoryStatus != DirectoryStatus.Removed))
            {
                // Base last-mod (existing logic)
                var baseLastMod = new[]
                {
                    entry.CreateDate,
                    entry.UpdateDate ?? entry.CreateDate,
                    mostRecentUpdateDate
                }.Max();

                // If there is an approved review newer than the entry's own dates, use it
                var reviewLastMod = latestApprovedReviewByEntry.TryGetValue(entry.DirectoryEntryId, out var rdt)
                    ? rdt
                    : DateTime.MinValue;

                var directoryItemLastMod = new[] { baseLastMod, reviewLastMod }.Max();

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
                    LastMod = directoryItemLastMod
                });
            }

            // Add /countries and per-country pages to the sitemap
            this.AddCountriesToSitemap(
                siteMapHelper,
                domain,
                allActiveEntries,
                latestApprovedReviewByEntry,
                mostRecentUpdateDate);

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

                var subCategories = await this.subCategoryRepository.GetActiveSubcategoriesAsync(category.CategoryId);

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

            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[Constants.StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "sitemap");
            return this.View("Index", model);
        }

        private async Task AddTags(DateTime mostRecentUpdateDate, SiteMapHelper siteMapHelper, string domain)
        {
            var tagPageSize = IntegerConstants.MaxPageSize;
            var tagsWithInfo = await this.tagRepository.ListTagsWithSitemapInfoAsync();

            foreach (var tag in tagsWithInfo)
            {
                int totalPages = (int)Math.Ceiling((double)tag.EntryCount / tagPageSize);

                for (int i = 1; i <= totalPages; i++)
                {
                    string url = i == 1
                        ? $"{domain}/tagged/{tag.Slug}"
                        : $"{domain}/tagged/{tag.Slug}/page/{i}";

                    siteMapHelper.SiteMapItems.Add(new SiteMapItem
                    {
                        Url = url,
                        Priority = i == 1 ? 0.5 : 0.3,
                        ChangeFrequency = ChangeFrequency.Weekly,
                        LastMod = tag.LastModified > mostRecentUpdateDate
                                    ? tag.LastModified
                                    : mostRecentUpdateDate
                    });
                }
            }

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/tagged", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Monthly,
                LastMod = mostRecentUpdateDate
            });
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
            Dictionary<int, int> subcategoryEntryCounts,
            string domain,
            Data.Models.Category category)
        {
            var lastChangeToCategory = allCategoriesLastModified.TryGetValue(category.CategoryId, out var categoryMod)
                ? categoryMod
                : DateTime.MinValue;

            lastChangeToCategory = mostRecentUpdateDate > lastChangeToCategory
                ? mostRecentUpdateDate
                : lastChangeToCategory;

            var subCategories = await this.subCategoryRepository.GetActiveSubcategoriesAsync(category.CategoryId);

            DateTime mostRecentSubcategoryDate = subCategories
                .Select(sub => new[]
                {
            allSubcategoriesLastModified.TryGetValue(sub.SubCategoryId, out var subMod) ? subMod : DateTime.MinValue,
            allSubCategoriesItemsLastModified.TryGetValue(sub.SubCategoryId, out var itemMod) ? itemMod : DateTime.MinValue,
            allSubCategoryAds.TryGetValue(sub.SubCategoryId, out var adMod) ? adMod : DateTime.MinValue
                }.Max())
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            DateTime mostRecentCategoryAdDate = allCategoryAds.TryGetValue(category.CategoryId, out var catAdMod)
                ? catAdMod
                : DateTime.MinValue;

            var lastChangeForCategoryOrSubcategory = new[]
            {
                lastChangeToCategory,
                mostRecentSubcategoryDate,
                mostRecentCategoryAdDate,
                mostRecentUpdateDate
            }.Max();

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = $"{domain}/{category.CategoryKey}",
                Priority = 0.6,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = lastChangeForCategoryOrSubcategory
            });

            foreach (var subCategory in subCategories)
            {
                this.AddSubcategoriesAsync(
                    lastFeaturedDate,
                    mostRecentUpdateDate,
                    siteMapHelper,
                    allSubcategoriesLastModified,
                    allSubCategoriesItemsLastModified,
                    allSubCategoryAds,
                    subcategoryEntryCounts,
                    category,
                    lastChangeToCategory,
                    subCategory,
                    domain);
            }
        }

        private void AddSubcategoriesAsync(
            DateTime lastFeaturedDate,
            DateTime mostRecentUpdateDate,
            SiteMapHelper siteMapHelper,
            Dictionary<int, DateTime> allSubcategoriesLastModified,
            Dictionary<int, DateTime> allSubCategoriesItemsLastModified,
            Dictionary<int, DateTime> allSubCategoryAds,
            Dictionary<int, int> subcategoryEntryCounts,
            Data.Models.Category category,
            DateTime lastChangeToCategory,
            Data.Models.Subcategory subCategory,
            string domain)
        {
            int subCategoryId = subCategory.SubCategoryId;

            DateTime lastChangeToSubcategory = allSubcategoriesLastModified.TryGetValue(subCategoryId, out var subMod)
                ? subMod
                : lastChangeToCategory;

            DateTime lastChangeToSubcategoryItem = allSubCategoriesItemsLastModified.TryGetValue(subCategoryId, out var itemMod)
                ? itemMod
                : lastChangeToSubcategory;

            DateTime lastChangeToSubcategoryAd = allSubCategoryAds.TryGetValue(subCategoryId, out var adMod)
                ? adMod
                : lastChangeToSubcategoryItem;

            DateTime lastModified = new[]
            {
                lastFeaturedDate,
                lastChangeToSubcategory,
                lastChangeToSubcategoryItem,
                lastChangeToSubcategoryAd,
                mostRecentUpdateDate
            }.Max().AddHours(1); // slight offset to ensure update pickup

            // Add base subcategory page
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = $"{domain}/{category.CategoryKey}/{subCategory.SubCategoryKey}",
                Priority = 0.5,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = lastModified
            });

            // Add paginated subcategory pages
            if (!subcategoryEntryCounts.TryGetValue(subCategoryId, out var entryCount))
            {
                return;
            }

            int pageSize = IntegerConstants.DefaultPageSize;
            int totalPages = (int)Math.Ceiling(entryCount / (double)pageSize);

            for (int i = 2; i <= totalPages; i++)
            {
                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = $"{domain}/{category.CategoryKey}/{subCategory.SubCategoryKey}/page/{i}",
                    Priority = 0.3,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMod = lastModified
                });
            }
        }

        private async Task AddNewestPagesListAsync(DateTime date, SiteMapHelper siteMapHelper)
        {
            int totalEntries = await this.directoryEntryRepository.TotalActive();
            int pageSize = IntegerConstants.MaxPageSize;
            int totalPages = (int)Math.Ceiling((double)totalEntries / pageSize);

            string domain = WebRequestHelper.GetCurrentDomain(this.HttpContext).TrimEnd('/');

            for (int i = 1; i <= totalPages; i++)
            {
                string url = i == 1
                    ? $"{domain}/newest"
                    : $"{domain}/newest/page/{i}";

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = url,
                    Priority = 0.4,
                    ChangeFrequency = ChangeFrequency.Daily,
                    LastMod = date
                });
            }
        }

        private async Task AddCategoryPaginationAsync(
            DateTime date,
            SiteMapHelper siteMapHelper,
            IEnumerable<Category> categories,
            Dictionary<int, int> subcategoryEntryCounts,
            string domain)
        {
            int pageSize = IntegerConstants.DefaultPageSize;

            // Get all counts at once
            var categoryCounts = await this.directoryEntryRepository.GetCategoryEntryCountsAsync();

            foreach (var category in categories)
            {
                if (!categoryCounts.TryGetValue(category.CategoryId, out int totalCount) || totalCount == 0)
                {
                    continue; // skip categories with no entries
                }

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                for (int i = 1; i <= totalPages; i++)
                {
                    string url = i == 1
                        ? $"{domain}/{category.CategoryKey}"
                        : $"{domain}/{category.CategoryKey}/page/{i}";

                    siteMapHelper.SiteMapItems.Add(new SiteMapItem
                    {
                        Url = url,
                        Priority = 0.4,
                        ChangeFrequency = ChangeFrequency.Weekly,
                        LastMod = date
                    });
                }
            }
        }

        private async Task AddPagesAsync(DateTime date, SiteMapHelper siteMapHelper)
        {
            var contactHtmlConfig = await this.contentSnippetRepository.GetAsync(SiteConfigSetting.ContactHtml);

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

            var donationHtmlConfig = await this.contentSnippetRepository.GetAsync(SiteConfigSetting.DonationHtml);

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
                Url = string.Format("{0}/faq", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.3,
                ChangeFrequency = ChangeFrequency.Weekly,
                LastMod = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/rss/feed.xml", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 0.9,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMod = date
            });
        }

        private void AddCountriesToSitemap(
            SiteMapHelper siteMapHelper,
            string domain,
            IEnumerable<DirectoryEntry> allEntries,
            IReadOnlyDictionary<int, DateTime> latestApprovedReviewByEntry,
            DateTime siteWideLastMod)
        {
            // Active statuses (mirror your definition)
            static bool IsActive(DirectoryStatus s) =>
                s == DirectoryStatus.Admitted
                || s == DirectoryStatus.Verified
                || s == DirectoryStatus.Scam
                || s == DirectoryStatus.Questionable;

            var countriesMap = CountryHelper.GetCountries(); // ISO2 -> Full Name

            // Only active entries with a known ISO2 code
            var activeWithCountry = allEntries
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed
                            && IsActive(e.DirectoryStatus)
                            && !string.IsNullOrWhiteSpace(e.CountryCode))
                .Select(e => new
                {
                    Entry = e,
                    Code = e.CountryCode!.Trim().ToUpperInvariant()
                })
                .Where(x => countriesMap.ContainsKey(x.Code))
                .ToList();

            if (activeWithCountry.Count == 0)
            {
                return;
            }

            // Group by country (we’ll use siteWideLastMod for all sitemap items)
            var byCountry = activeWithCountry
                .GroupBy(x => x.Code)
                .Select(g =>
                {
                    string code = g.Key;
                    string name = CountryHelper.GetCountryName(code);
                    string slug = StringHelpers.UrlKey(name);
                    int count = g.Count();

                    return new
                    {
                        Code = code,
                        Name = name,
                        Slug = slug,
                        Count = count
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();

            // -------- /countries (index) + pagination --------
            int countriesPageSize = IntegerConstants.MaxPageSize;
            int countriesTotal = byCountry.Count;
            int countriesPages = (int)Math.Ceiling(countriesTotal / (double)countriesPageSize);

            for (int page = 1; page <= countriesPages; page++)
            {
                string url = page == 1
                    ? $"{domain}/countries"
                    : $"{domain}/countries/page/{page}";

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = url,
                    Priority = page == 1 ? 0.3 : 0.2,
                    ChangeFrequency = ChangeFrequency.Monthly,
                    LastMod = siteWideLastMod
                });
            }

            // -------- /countries/{slug} + pagination --------
            int countryEntriesPageSize = IntegerConstants.DefaultPageSize; // match listing page size

            foreach (var c in byCountry)
            {
                if (c.Count <= 0)
                {
                    continue;
                }

                int pages = (int)Math.Ceiling(c.Count / (double)countryEntriesPageSize);

                // page 1
                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = $"{domain}/countries/{c.Slug}",
                    Priority = 0.5,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMod = siteWideLastMod
                });

                // pages 2..N
                for (int i = 2; i <= pages; i++)
                {
                    siteMapHelper.SiteMapItems.Add(new SiteMapItem
                    {
                        Url = $"{domain}/countries/{c.Slug}/page/{i}",
                        Priority = 0.3,
                        ChangeFrequency = ChangeFrequency.Weekly,
                        LastMod = siteWideLastMod
                    });
                }
            }
        }

        private async Task AddAllTagsPaginationAsync(DateTime date, SiteMapHelper siteMapHelper)
        {
            var domain = WebRequestHelper.GetCurrentDomain(this.HttpContext).TrimEnd('/');
            int pageSize = IntegerConstants.MaxPageSize;

            // Get the paged result, which includes the total count
            var pagedResult = await this.tagRepository.ListTagsWithCountsPagedAsync(1, int.MaxValue).ConfigureAwait(false);

            // Use the total count of active tags for pagination
            int totalTags = pagedResult.TotalCount;
            int totalPages = (totalTags + pageSize - 1) / pageSize; // Calculate total pages

            for (int page = 1; page <= totalPages; page++)
            {
                string url = page == 1
                    ? $"{domain}/tagged"
                    : $"{domain}/tagged/page/{page}";

                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = url,
                    Priority = page == 1 ? 0.3 : 0.2,
                    ChangeFrequency = ChangeFrequency.Monthly,
                    LastMod = date
                });
            }
        }

        private DateTime GetLatestUpdateDate(
            DateTime? lastDirectoryEntryDate,
            DateTime? lastContentSnippetUpdate,
            DateTime? lastPaidInvoiceUpdate,
            DateTime? nextAdExpiration,
            DateTime? lastMainSponsorExpiration,
            bool isAdSpaceAvailable)
        {
            DateTime? latestUpdateDate = null;

            // Step 1: Find most recent general update
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

            // Step 2: Include next expiration only if same day or no other updates
            if (nextAdExpiration.HasValue)
            {
                if (!latestUpdateDate.HasValue || nextAdExpiration.Value.Date == latestUpdateDate.Value.Date)
                {
                    latestUpdateDate = nextAdExpiration;
                }
            }

            // Step 3: If ad space is available and there was a recent expiration, use that
            if (isAdSpaceAvailable && lastMainSponsorExpiration.HasValue)
            {
                if (!latestUpdateDate.HasValue || lastMainSponsorExpiration > latestUpdateDate)
                {
                    latestUpdateDate = lastMainSponsorExpiration.Value.AddMinutes(1); // offset by 1 min for freshness
                }
            }

            return latestUpdateDate ?? DateTime.MinValue;
        }
    }
}