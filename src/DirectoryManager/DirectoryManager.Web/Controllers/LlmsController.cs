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
            // Scope the cache key to a content-version stamp so the file is regenerated the moment
            // the directory changes — and NOT before. A fixed 1-hour TTL used to let the cache lag
            // behind a change, so the file would silently re-generate (and get re-mirrored to the
            // CDN) at a time when the sitemap's <lastmod> showed nothing new — the "files updated
            // but nothing changed" churn. The version stamp advances on exactly the same signals
            // that move the sitemap, so llms.txt now changes only in lockstep with a real change.
            var version = await this.GetContentVersionAsync();
            var content = await this.cache.GetOrCreateAsync($"{StringConstants.CacheKeyLlmsTxt}:{version}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6); // safety net; the key self-invalidates on change
                return await this.BuildLlmsTxtAsync();
            }) ?? string.Empty;

            // The static CDN mirror serves llms.txt as text/plain with NO charset, so browsers
            // fall back to Latin-1 and show mojibake (e.g. "â€"") for UTF-8 punctuation. Keep the
            // output ASCII-only so it renders correctly regardless of the declared charset.
            content = content
                .Replace('—', '-')   // em dash —
                .Replace('–', '-')   // en dash –
                .Replace('‘', '\'')  // '
                .Replace('’', '\'')  // '
                .Replace('“', '"')   // "
                .Replace('”', '"')   // "
                .Replace("…", "...")  // …
                .Replace(' ', ' ');  // non-breaking space

            return this.Content(content, "text/plain", Encoding.UTF8);
        }

        [Route("llms-full.txt")]
        [HttpGet]
        public async Task<ContentResult> LlmsFullTxt()
        {
            var version = await this.GetContentVersionAsync();
            var content = await this.cache.GetOrCreateAsync($"{StringConstants.CacheKeyLlmsFullTxt}:{version}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6); // safety net; the key self-invalidates on change
                return await this.BuildLlmsFullTxtAsync();
            }) ?? string.Empty;

            return this.Content(AsciiClean(content), "text/plain", Encoding.UTF8);
        }

        /// <summary>
        /// A cheap token that changes whenever anything the llms files render changes: the newest
        /// directory-entry create/edit, the newest category or subcategory add/rename, and the live
        /// active-listing count (which catches scheduled/sponsor activations that flip an entry in
        /// or out without touching its UpdateDate). Every one of these signals also advances the
        /// sitemap's max &lt;lastmod&gt;, so folding it into the cache key keeps llms.txt / llms-full.txt
        /// byte-stable until — and only until — a real change the mirror should pick up.
        /// </summary>
        private async Task<string> GetContentVersionAsync()
        {
            var lastEntryRevision = this.directoryEntryRepository.GetLastRevisionDate();
            var categoryDates = await this.categoryRepository.GetAllCategoriesLastChangeDatesAsync();
            var subcategoryDates = await this.subCategoryRepository.GetAllSubCategoriesLastChangeDatesAsync();
            var totalActive = await this.directoryEntryRepository.TotalActive();

            var latest = new[]
            {
                lastEntryRevision,
                categoryDates.Values.DefaultIfEmpty(DateTime.MinValue).Max(),
                subcategoryDates.Values.DefaultIfEmpty(DateTime.MinValue).Max(),
            }.Max();

            return $"{latest.Ticks}-{totalActive}";
        }

        // The static CDN mirror serves these as text/plain with NO charset, so browsers fall back
        // to Latin-1 and show mojibake for UTF-8 punctuation. Keep the output ASCII-only.
        private static string AsciiClean(string content) => content
            .Replace('—', '-')   // em dash
            .Replace('–', '-')   // en dash
            .Replace('‘', '\'')  // left single quote
            .Replace('’', '\'')  // right single quote
            .Replace('“', '"')   // left double quote
            .Replace('”', '"')   // right double quote
            .Replace("…", "...") // ellipsis
            .Replace(' ', ' ');  // non-breaking space

        private async Task<string> BuildLlmsFullTxtAsync()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain))
                .TrimEnd('/');

            var totalActive = await this.directoryEntryRepository.TotalActive();

            // All live entries (ActiveQuery already drops Removed/Unknown); also drop Scam so
            // we never feed scam listings to an LLM as recommendations.
            var entries = (await this.directoryEntryRepository.GetAllActiveEntries())
                .Where(e => e.DirectoryStatus != DirectoryStatus.Scam && e.SubCategory?.Category != null)
                .ToList();

            var sb = new StringBuilder();

            sb.AppendLine("# Monerica - Full Directory");
            sb.AppendLine($"> Every live listing in the Monerica directory ({totalActive} websites and services that accept or relate to Monero (XMR)).");
            sb.AppendLine($"> {domain}");
            sb.AppendLine($"> For the concise category map, see {domain}/llms.txt");
            sb.AppendLine();

            foreach (var catGroup in entries
                .GroupBy(e => e.SubCategory!.Category!.Name)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"## {catGroup.Key}");

                foreach (var subGroup in catGroup
                    .GroupBy(e => e.SubCategory!.Name)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"### {subGroup.Key}");

                    foreach (var e in subGroup.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var status = e.DirectoryStatus switch
                        {
                            DirectoryStatus.Verified => "Verified. ",
                            DirectoryStatus.Questionable => "Questionable. ",
                            _ => string.Empty,
                        };

                        var desc = string.IsNullOrWhiteSpace(e.Description)
                            ? string.Empty
                            : System.Text.RegularExpressions.Regex.Replace(e.Description.Trim(), "\\s+", " ");

                        var line = $"- [{e.Name}]({domain}/site/{e.DirectoryEntryKey}): {status}{desc}";
                        if (!string.IsNullOrWhiteSpace(e.Link))
                        {
                            line += $" (site: {e.Link})";
                        }

                        sb.AppendLine(line);
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
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
                    sb.AppendLine($"- [{sub.Name}]({domain}/{category.CategoryKey}/{sub.SubCategoryKey}): {category.Name} — {sub.Name} that accept Monero.");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}