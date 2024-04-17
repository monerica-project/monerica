﻿using DirectoryManager.Data.Repositories.Implementations;
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

        public SiteMapController(
            ICacheService cacheService,
            IMemoryCache memoryCache,
            IDirectoryEntryRepository directoryEntryRepository,
            ICategoryRepository categoryRepository,
            ISubCategoryRepository subCategoryRepository)
        {
            this.cacheService = cacheService;
            this.memoryCache = memoryCache;
            this.directoryEntryRepository = directoryEntryRepository;
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
        }

        [Route("sitemap_index.xml")]
        public IActionResult SiteMapIndex()
        {
            return this.RedirectPermanent("~/sitemap.xml");
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> IndexAsync()
        {
            var date = this.directoryEntryRepository.GetLastRevisionDate();
            var siteMapHelper = new SiteMapHelper();
            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = WebRequestHelper.GetCurrentDomain(this.HttpContext),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
            });

            siteMapHelper.SiteMapItems.Add(new SiteMapItem
            {
                Url = string.Format("{0}/newest", WebRequestHelper.GetCurrentDomain(this.HttpContext)),
                Priority = 1.0,
                ChangeFrequency = ChangeFrequency.Daily,
                LastMode = date
            });

            var categories = await this.categoryRepository.GetActiveCategoriesAsync();

            foreach (var category in categories)
            {
                siteMapHelper.SiteMapItems.Add(new SiteMapItem
                {
                    Url = string.Format("{0}/{1}", WebRequestHelper.GetCurrentDomain(this.HttpContext), category.CategoryKey),
                    Priority = 1.0,
                    ChangeFrequency = ChangeFrequency.Weekly,
                    LastMode = date
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
                        LastMode = date
                    });
                }
            }

            var xml = siteMapHelper.GenerateXml();

            return this.Content(xml, "text/xml");
        }
    }
}