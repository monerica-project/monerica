using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Utilities.Helpers;
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
        private readonly IDirectoryEntryReviewRepository reviewRepository;

        public LlmsController(
            ICategoryRepository categoryRepository,
            ISubcategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ICacheService cacheService,
            IMemoryCache cache,
            IDirectoryEntryReviewRepository reviewRepository)
        {
            this.reviewRepository = reviewRepository;
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
        /// Every live listing as structured JSON, for AI tools, search engines and anyone who wants the
        /// data (the machine-readable twin of llms-full.txt). Scam listings are included with
        /// status "Scam" so tools can warn people, but WITHOUT any outbound links.
        /// </summary>
        [Route("data.json")]
        [HttpGet]
        public async Task<ContentResult> DataJson()
        {
            var version = await this.GetContentVersionAsync();
            var content = await this.cache.GetOrCreateAsync($"{CacheKeyDataJson}:{version}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6); // safety net; the key self-invalidates on change
                return await this.BuildDataJsonAsync();
            }) ?? "{}";

            return this.Content(content, "application/json", Encoding.UTF8);
        }

        /// <summary>The newest listings as a JSON Feed 1.1 (https://jsonfeed.org/version/1.1).</summary>
        [Route("feed.json")]
        [HttpGet]
        public async Task<ContentResult> FeedJson()
        {
            var version = await this.GetContentVersionAsync();
            var content = await this.cache.GetOrCreateAsync($"{CacheKeyFeedJson}:{version}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                return await this.BuildFeedJsonAsync();
            }) ?? "{}";

            return this.Content(content, "application/feed+json", Encoding.UTF8);
        }

        /// <summary>
        /// RFC 9727 API catalog: tells crawlers and AI tools where the machine-readable data lives.
        /// </summary>
        [Route(".well-known/api-catalog")]
        [HttpGet]
        public async Task<ContentResult> ApiCatalog()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain)).TrimEnd('/');
            var catalog = new
            {
                linkset = new[]
                {
                    new
                    {
                        anchor = $"{domain}/.well-known/api-catalog",
                        item = new[]
                        {
                            new { href = $"{domain}/data.json", type = "application/json", title = "Every live listing as structured data" },
                            new { href = $"{domain}/feed.json", type = "application/feed+json", title = "Newest listings (JSON Feed)" },
                            new { href = $"{domain}/rss/feed.xml", type = "application/rss+xml", title = "Newest listings (RSS)" },
                            new { href = $"{domain}/llms.txt", type = "text/plain", title = "Category map for AI tools" },
                            new { href = $"{domain}/llms-full.txt", type = "text/plain", title = "Every listing as plain text for AI tools" },
                            new { href = $"{domain}/sitemap.xml", type = "application/xml", title = "Sitemap" },
                        },
                    },
                },
            };

            return this.Content(JsonSerializer.Serialize(catalog, JsonOptions), "application/linkset+json", Encoding.UTF8);
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

            // llms-full.txt shows ratings, so an approved review changes it too (reviews also
            // advance the sitemap max, so the lockstep with the mirror is kept).
            var reviewDates = await this.reviewRepository.GetApprovedReviewLastModifiedByEntryAsync();

            var latest = new[]
            {
                lastEntryRevision,
                categoryDates.Values.DefaultIfEmpty(DateTime.MinValue).Max(),
                subcategoryDates.Values.DefaultIfEmpty(DateTime.MinValue).Max(),
                reviewDates.Values.DefaultIfEmpty(DateTime.MinValue).Max(),
            }.Max();

            return $"{latest.Ticks}-{totalActive}";
        }

        private const string CacheKeyDataJson = "machine-data-json";
        private const string CacheKeyFeedJson = "machine-feed-json";
        private const int FeedItemCount = 50;

        private static readonly JsonSerializerOptions JsonOptions = new ()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Readable output ("+", accents, non-Latin names) — this is served as JSON, never inlined into HTML.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private async Task<string> BuildDataJsonAsync()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain)).TrimEnd('/');
            var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            var currency = await this.cacheService.GetSnippetAsync(SiteConfigSetting.DirectoryCurrencyName);
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var entries = (await this.directoryEntryRepository.GetAllActiveEntries())
                .Where(e => e.SubCategory?.Category != null)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var ratings = await this.reviewRepository.GetRatingSummariesAsync(
                entries.Select(e => e.DirectoryEntryId).Distinct().ToList());

            var listings = entries.Select(e =>
            {
                var isScam = e.DirectoryStatus == DirectoryStatus.Scam;
                ratings.TryGetValue(e.DirectoryEntryId, out var rating);
                var otherLinks = new List<object>();
                if (!isScam && !string.IsNullOrWhiteSpace(e.Link2))
                {
                    otherLinks.Add(new { label = string.IsNullOrWhiteSpace(link2Name) ? "Link 2" : link2Name.Trim(), url = e.Link2 });
                }

                if (!isScam && !string.IsNullOrWhiteSpace(e.Link3))
                {
                    otherLinks.Add(new { label = string.IsNullOrWhiteSpace(link3Name) ? "Link 3" : link3Name.Trim(), url = e.Link3 });
                }

                return new
                {
                    name = e.Name,
                    slug = e.DirectoryEntryKey,
                    profileUrl = $"{domain}/site/{e.DirectoryEntryKey}",
                    website = isScam ? null : NullIfBlank(e.Link),
                    otherLinks = otherLinks.Count > 0 ? otherLinks : null,
                    category = e.SubCategory!.Category!.Name,
                    subcategory = e.SubCategory!.Name,
                    categoryUrl = $"{domain}/{e.SubCategory!.Category!.CategoryKey}/{e.SubCategory!.SubCategoryKey}",
                    status = e.DirectoryStatus.ToString(),
                    warning = isScam ? "Listed as a scam: do not use." : null,
                    kycPolicy = e.KycPolicy is { } k && k != KycPolicy.Unknown ? EnumHelper.GetDescription(k) : null,
                    liquidity = e.Liquidity is Liquidity.Own or Liquidity.Mixed or Liquidity.ThirdParty
                        or Liquidity.OwnAndThirdParty or Liquidity.VariesByProvider
                        ? EnumHelper.GetDescription(e.Liquidity)
                        : null,
                    rates = e.RateType != RateType.Unknown ? EnumHelper.GetDescription(e.RateType) : null,
                    tags = e.EntryTags?.Where(t => t.Tag != null).Select(t => t.Tag.Name).OrderBy(n => n).ToList() is { Count: > 0 } tg ? tg : null,
                    country = NullIfBlank(e.CountryCode),
                    founded = e.FoundedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    description = string.IsNullOrWhiteSpace(e.Description)
                        ? null
                        : System.Text.RegularExpressions.Regex.Replace(e.Description.Trim(), "\\s+", " "),
                    rating = rating != null && rating.ReviewCount > 0
                        ? new { average = Math.Round(rating.AvgRating, 1), reviews = rating.ReviewCount }
                        : null,
                    added = e.CreateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    updated = (e.UpdateDate ?? e.CreateDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                };
            }).ToList();

            var doc = new
            {
                name = siteName,
                description = $"Every live listing in the {siteName} directory{AcceptOrRelateClause(currency)}.",
                homepage = domain,
                generated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                count = listings.Count,
                statuses = new
                {
                    Verified = "Tested by at least one member of the community.",
                    Admitted = "Added to the directory but not yet tested by the community.",
                    Questionable = "Doubts about its claims or reliability, but no confirmed reports of theft.",
                    Scam = "Credible evidence it has taken users' funds without delivering. Outbound links are omitted.",
                },
                otherFormats = new
                {
                    llmsTxt = $"{domain}/llms.txt",
                    llmsFullTxt = $"{domain}/llms-full.txt",
                    feed = $"{domain}/feed.json",
                },
                listings,
            };

            return JsonSerializer.Serialize(doc, JsonOptions);
        }

        private async Task<string> BuildFeedJsonAsync()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain)).TrimEnd('/');
            var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            var currency = await this.cacheService.GetSnippetAsync(SiteConfigSetting.DirectoryCurrencyName);

            var newest = (await this.directoryEntryRepository.GetAllActiveEntries())
                .Where(e => e.DirectoryStatus != DirectoryStatus.Scam && e.SubCategory?.Category != null)
                .OrderByDescending(e => e.CreateDate)
                .Take(FeedItemCount)
                .ToList();

            var feed = new
            {
                version = "https://jsonfeed.org/version/1.1",
                title = $"{siteName} - newest listings",
                home_page_url = domain,
                feed_url = $"{domain}/feed.json",
                description = $"The newest websites and services added to {siteName}{AcceptOrRelateClause(currency)}.",
                items = newest.Select(e => new
                {
                    id = $"{domain}/site/{e.DirectoryEntryKey}",
                    url = $"{domain}/site/{e.DirectoryEntryKey}",
                    external_url = NullIfBlank(e.Link),
                    title = e.Name,
                    content_text = string.IsNullOrWhiteSpace(e.Description) ? e.Name : e.Description.Trim(),
                    date_published = DateTime.SpecifyKind(e.CreateDate, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    tags = new[] { e.SubCategory!.Category!.Name, e.SubCategory!.Name },
                }).ToList(),
            };

            return JsonSerializer.Serialize(feed, JsonOptions);
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        // " that accept or relate to Monero (XMR)" from the DirectoryCurrencyName snippet
        // (config, not hardcoded — the codebase is forked); empty when the snippet isn't set.
        internal static string AcceptOrRelateClause(string? currencyName) =>
            string.IsNullOrWhiteSpace(currencyName)
                ? string.Empty
                : $" that accept or relate to {currencyName.Trim()}";

        // " that accept Monero": the short form is the part before " (" ("Monero (XMR)" -> "Monero").
        internal static string AcceptShortClause(string? currencyName)
        {
            if (string.IsNullOrWhiteSpace(currencyName))
            {
                return string.Empty;
            }

            var name = currencyName.Trim();
            var paren = name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0)
            {
                name = name.Substring(0, paren).Trim();
            }

            return $" that accept {name}";
        }

        /// <summary>
        /// The facts line for one listing in llms-full.txt, e.g.
        /// "KYC: Guaranteed No KYC; Liquidity: Own; Tor: yes; Rating: 4.7/5 (31 reviews)".
        /// Unknown values are left out rather than guessed.
        /// </summary>
        internal static string ListingFacts(
            DirectoryEntry entry,
            string? link2Name,
            string? link3Name,
            double? averageRating,
            int ratedReviewCount)
        {
            var facts = new List<string>();

            if (entry.KycPolicy is { } kyc && kyc != KycPolicy.Unknown)
            {
                facts.Add($"KYC: {EnumHelper.GetDescription(kyc)}");
            }

            if (entry.Liquidity is Liquidity.Own or Liquidity.Mixed or Liquidity.ThirdParty
                or Liquidity.OwnAndThirdParty or Liquidity.VariesByProvider)
            {
                facts.Add($"Liquidity: {EnumHelper.GetDescription(entry.Liquidity)}");
            }

            if (entry.RateType != RateType.Unknown)
            {
                facts.Add($"Rates: {EnumHelper.GetDescription(entry.RateType)}");
            }

            if (!string.IsNullOrWhiteSpace(link2Name))
            {
                facts.Add($"{link2Name.Trim()}: {(string.IsNullOrWhiteSpace(entry.Link2) ? "no" : "yes")}");
            }

            if (!string.IsNullOrWhiteSpace(link3Name))
            {
                facts.Add($"{link3Name.Trim()}: {(string.IsNullOrWhiteSpace(entry.Link3) ? "no" : "yes")}");
            }

            if (ratedReviewCount > 0 && averageRating.HasValue)
            {
                var avg = Math.Round(averageRating.Value, 1).ToString("0.0", CultureInfo.InvariantCulture);
                facts.Add($"Rating: {avg}/5 ({ratedReviewCount} review{(ratedReviewCount == 1 ? string.Empty : "s")})");
            }
            else
            {
                facts.Add("Rating: no reviews yet");
            }

            return string.Join("; ", facts);
        }

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

            var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            var currency = await this.cacheService.GetSnippetAsync(SiteConfigSetting.DirectoryCurrencyName);
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var ratings = await this.reviewRepository.GetRatingSummariesAsync(
                entries.Select(e => e.DirectoryEntryId).Distinct().ToList());

            var sb = new StringBuilder();

            sb.AppendLine($"# {siteName} - Full Directory");
            sb.AppendLine($"> Every live listing in the {siteName} directory ({totalActive} websites and services{AcceptOrRelateClause(currency)}).");
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

                        ratings.TryGetValue(e.DirectoryEntryId, out var rating);
                        line += $" [{ListingFacts(e, link2Name, link3Name, rating?.AvgRating, rating?.ReviewCount ?? 0)}]";

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
            var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            var currency = await this.cacheService.GetSnippetAsync(SiteConfigSetting.DirectoryCurrencyName);
            var acceptShort = AcceptShortClause(currency);

            var sb = new StringBuilder();

            sb.AppendLine($"# {siteName}");
            sb.AppendLine($"> A curated directory of {totalActive} websites and services{AcceptOrRelateClause(currency)}.");
            sb.AppendLine($"> {domain}");
            sb.AppendLine($"> Every listing with key facts: {domain}/llms-full.txt");
            sb.AppendLine($"> Structured data (JSON): {domain}/data.json");
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
                    // The subcategory's own meta description when it has one; boilerplate otherwise.
                    var summary = string.IsNullOrWhiteSpace(sub.MetaDescription)
                        ? $"{category.Name} — {sub.Name}{acceptShort}."
                        : System.Text.RegularExpressions.Regex.Replace(sub.MetaDescription.Trim(), "\\s+", " ");
                    sb.AppendLine($"- [{sub.Name}]({domain}/{category.CategoryKey}/{sub.SubCategoryKey}): {summary}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}