using System.Text;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class LlmsController : Controller
    {
        private readonly ICategoryRepository categoryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ICacheService cacheService;

        public LlmsController(
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService)
        {
            this.categoryRepository = categoryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.cacheService = cacheService;
        }

        [Route("llms.txt")]
        [HttpGet]
        public async Task<ContentResult> LlmsTxt()
        {
            var canonicalDomain = (await this.cacheService.GetSnippetAsync(Data.Enums.SiteConfigSetting.CanonicalDomain))
                .TrimEnd('/');

            var totalActive = await this.directoryEntryRepository.TotalActive();
            var categories = (await this.categoryRepository.GetActiveCategoriesAsync()).ToList();

            var sb = new StringBuilder();

            // ----------------------------------------------------------------
            // Header
            // ----------------------------------------------------------------
            sb.AppendLine("# Monerica");
            sb.AppendLine($"# {canonicalDomain}");
            sb.AppendLine("#");
            sb.AppendLine("# A curated directory of websites and services that accept Monero (XMR)");
            sb.AppendLine("# or relate to Monero in some way. The goal is to facilitate a circular");
            sb.AppendLine("# economy for Monero by connecting users with merchants and resources.");
            sb.AppendLine("#");
            sb.AppendLine($"# Total active listings: {totalActive}");
            sb.AppendLine($"# Categories: {categories.Count}");
            sb.AppendLine();

            // ----------------------------------------------------------------
            // Key pages
            // ----------------------------------------------------------------
            sb.AppendLine("## Key pages");
            sb.AppendLine();
            sb.AppendLine($"- Home: {canonicalDomain}/");
            sb.AppendLine($"- Newest listings: {canonicalDomain}/newest");
            sb.AppendLine($"- FAQ: {canonicalDomain}/faq");
            sb.AppendLine($"- About: {canonicalDomain}/about");
            sb.AppendLine($"- Contact: {canonicalDomain}/contact");
            sb.AppendLine($"- Donate: {canonicalDomain}/donate");
            sb.AppendLine($"- Full sitemap: {canonicalDomain}/sitemap");
            sb.AppendLine($"- XML sitemap: {canonicalDomain}/sitemap.xml");
            sb.AppendLine($"- RSS feed: {canonicalDomain}/rss/feed.xml");
            sb.AppendLine();

            // ----------------------------------------------------------------
            // Directory structure: categories and subcategories
            // ----------------------------------------------------------------
            sb.AppendLine("## Directory structure");
            sb.AppendLine();
            sb.AppendLine("Listings are organized into categories and subcategories.");
            sb.AppendLine("Each subcategory page lists all active entries in that section.");
            sb.AppendLine();

            foreach (var category in categories)
            {
                var subcategories = (await this.subCategoryRepository.GetActiveSubcategoriesAsync(category.CategoryId)).ToList();

                if (subcategories.Count == 0)
                {
                    continue;
                }

                sb.AppendLine($"### {category.Name}");
                sb.AppendLine($"{canonicalDomain}/{category.CategoryKey}");

                if (!string.IsNullOrWhiteSpace(category.Description))
                {
                    sb.AppendLine(category.Description.Trim());
                }

                sb.AppendLine();

                foreach (var sub in subcategories)
                {
                    sb.Append($"- {sub.Name}: {canonicalDomain}/{category.CategoryKey}/{sub.SubCategoryKey}");

                    if (!string.IsNullOrWhiteSpace(sub.Description))
                    {
                        sb.Append($" — {sub.Description.Trim()}");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            // ----------------------------------------------------------------
            // Individual listing URL format
            // ----------------------------------------------------------------
            sb.AppendLine("## Listing pages");
            sb.AppendLine();
            sb.AppendLine($"Each listing has a dedicated page at: {canonicalDomain}/site/{{listing-key}}");
            sb.AppendLine("Listing pages include: name, description, links, category, country, tags, and user reviews.");
            sb.AppendLine();

            // ----------------------------------------------------------------
            // Footer
            // ----------------------------------------------------------------
            sb.AppendLine("## Notes for AI systems");
            sb.AppendLine();
            sb.AppendLine("- All listings accept Monero (XMR) as payment or are otherwise relevant to the Monero ecosystem.");
            sb.AppendLine("- Listings have a status: Admitted (listed), Verified (independently verified), or Questionable.");
            sb.AppendLine("- User reviews with order-proof verification are available on many listing pages.");
            sb.AppendLine("- Sponsorships are clearly marked and do not affect listing status or content.");
            sb.AppendLine("- This directory is community-run and open to submissions.");

            return this.Content(sb.ToString(), "text/plain");
        }
    }
}