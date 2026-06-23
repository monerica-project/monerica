using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class LlmsController : Controller
    {
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;

        public LlmsController(
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService,
            IMemoryCache cache)
        {
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheService = cacheService;
            this.cache = cache;
        }

        [Route("llms.txt")]
        [HttpGet]
        public async Task<ContentResult> LlmsTxt()
        {
            var content = await this.cache.GetOrCreateAsync(StringConstants.CacheKeyLlmsTxt, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await this.BuildLlmsTxtAsync();
            }) ?? string.Empty;

            return this.Content(content, "text/plain", Encoding.UTF8);
        }

        private async Task<string> BuildLlmsTxtAsync()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain))
                .TrimEnd('/');

            var totalActive = await this.directoryEntryRepository.TotalActive();
            var categories = (await this.categoryRepository.GetActiveCategoriesAsync()).ToList();

            var sb = new StringBuilder();

            sb.AppendLine("# Monerica");
            sb.AppendLine($"> A curated directory of {totalActive} websites and services that accept or relate to Monero (XMR).");
            sb.AppendLine($"> {domain}");
            sb.AppendLine();

            foreach (var category in categories)
            {
                var subcategories = (await this.subCategoryRepository.GetActiveSubcategoriesAsync(category.CategoryId)).ToList();

                if (subcategories.Count == 0)
                {
                    continue;
                }

                sb.AppendLine($"## {category.Name}");

                foreach (var sub in subcategories)
                {
                    sb.AppendLine($"- {sub.Name}: {domain}/{category.CategoryKey}/{sub.SubCategoryKey}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}