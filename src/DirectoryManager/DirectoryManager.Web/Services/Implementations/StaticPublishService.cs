using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.StaticPublishing;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Services.Implementations
{
    public class StaticPublishService : IStaticPublishService
    {
        private readonly ApplicationDbContext db;
        private readonly IStaticPublishRepository queue;
        private readonly IDirectoryEntryRepository entries;
        private readonly ITagRepository tags;
        private readonly IOptionsMonitor<StaticPublishOptions> options;
        private readonly ILogger<StaticPublishService> logger;

        public StaticPublishService(
            ApplicationDbContext db,
            IStaticPublishRepository queue,
            IDirectoryEntryRepository entries,
            ITagRepository tags,
            IOptionsMonitor<StaticPublishOptions> options,
            ILogger<StaticPublishService> logger)
        {
            this.db = db;
            this.queue = queue;
            this.entries = entries;
            this.tags = tags;
            this.options = options;
            this.logger = logger;
        }

        public StaticPublishMode Mode => this.options.CurrentValue.Mode;

        private bool Enabled => this.Mode != StaticPublishMode.Off;

        public async Task<IReadOnlyList<string>> PathsForEntryAsync(int directoryEntryId, CancellationToken ct = default)
        {
            if (!this.Enabled)
            {
                return Array.Empty<string>();
            }

            try
            {
                var facts = await this.LoadFactsAsync(directoryEntryId, ct);
                return facts == null ? Array.Empty<string>() : StaticPublishPathMap.ForListing(facts);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not list pages for listing {Id}.", directoryEntryId);
                return Array.Empty<string>();
            }
        }

        public async Task EnqueueForEntryAsync(int directoryEntryId, string reason, IEnumerable<string>? pathsBefore = null, CancellationToken ct = default)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                var facts = await this.LoadFactsAsync(directoryEntryId, ct);
                var after = facts == null ? new List<string>() : StaticPublishPathMap.ForListing(facts);
                await this.EnqueueAsync(StaticPublishPathMap.Union(pathsBefore, after), reason, ct);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not queue listing {Id}.", directoryEntryId);
            }
        }

        public async Task EnqueueForReviewAsync(int directoryEntryReviewId, string reason, CancellationToken ct = default)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                var entryId = await this.db.DirectoryEntryReviews.AsNoTracking()
                    .Where(r => r.DirectoryEntryReviewId == directoryEntryReviewId)
                    .Select(r => (int?)r.DirectoryEntryId)
                    .FirstOrDefaultAsync(ct);
                if (entryId.HasValue)
                {
                    await this.EnqueueForEntryAsync(entryId.Value, reason, null, ct);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not queue review {Id}.", directoryEntryReviewId);
            }
        }

        public async Task EnqueueForReplyAsync(int directoryEntryReviewCommentId, string reason, CancellationToken ct = default)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                var reviewId = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                    .Where(c => c.DirectoryEntryReviewCommentId == directoryEntryReviewCommentId)
                    .Select(c => (int?)c.DirectoryEntryReviewId)
                    .FirstOrDefaultAsync(ct);
                if (reviewId.HasValue)
                {
                    await this.EnqueueForReviewAsync(reviewId.Value, reason, ct);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not queue reply {Id}.", directoryEntryReviewCommentId);
            }
        }

        public async Task EnqueueAsync(IEnumerable<string> paths, string reason, CancellationToken ct = default)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                var valid = paths.Where(p => StaticPublishPathRules.IsValidPath(p, out _)).ToList();
                await this.queue.EnqueueAsync(valid, reason, StaticPublishConstants.SourceInstant, DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not queue pages ({Reason}).", reason);
            }
        }

        public async Task RequestFullCrawlAsync(string reason, CancellationToken ct = default, bool runNow = false)
        {
            if (!this.Enabled)
            {
                return;
            }

            try
            {
                await this.queue.EnqueueAsync(
                    new[] { StaticPublishConstants.FullCrawlPath },
                    reason,
                    runNow ? StaticPublishConstants.SourceForced : StaticPublishConstants.SourceInstant,
                    DateTime.UtcNow,
                    ct);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Static publish: could not request a full crawl ({Reason}).", reason);
            }
        }

        /// <summary>Loads the facts for <see cref="StaticPublishPathMap.ForListing"/> (null = no such listing).</summary>
        internal async Task<ListingPageFacts?> LoadFactsAsync(int directoryEntryId, CancellationToken ct)
        {
            var entry = await this.db.DirectoryEntries.AsNoTracking()
                .Where(e => e.DirectoryEntryId == directoryEntryId)
                .Select(e => new
                {
                    e.DirectoryEntryKey,
                    e.SubCategoryId,
                    e.CountryCode,
                })
                .FirstOrDefaultAsync(ct);
            if (entry == null)
            {
                return null;
            }

            var subcategoryIds = await this.db.DirectoryEntryAdditionalSubcategories.AsNoTracking()
                .Where(a => a.DirectoryEntryId == directoryEntryId)
                .Select(a => a.SubCategoryId)
                .ToListAsync(ct);
            subcategoryIds.Insert(0, entry.SubCategoryId);

            var subs = await this.db.Subcategories.AsNoTracking()
                .Where(s => subcategoryIds.Contains(s.SubCategoryId))
                .Select(s => new { s.SubCategoryId, s.SubCategoryKey, s.CategoryId, s.Category.CategoryKey })
                .ToListAsync(ct);

            var subCounts = await this.entries.GetSubcategoryEntryCountsAsync();
            var catCounts = await this.entries.GetCategoryEntryCountsAsync();

            var facts = new ListingPageFacts
            {
                EntryKey = entry.DirectoryEntryKey,
                CrowdReviewCount = await this.db.DirectoryEntryReviews.AsNoTracking()
                    .CountAsync(r => r.DirectoryEntryId == directoryEntryId
                        && r.ModerationStatus == ReviewModerationStatus.Approved
                        && !r.IsOfficial, ct),
            };

            foreach (var id in subcategoryIds.Distinct())
            {
                var s = subs.FirstOrDefault(x => x.SubCategoryId == id);
                if (s == null)
                {
                    continue;
                }

                facts.Placements.Add(new ListingPlacementFacts
                {
                    CategoryKey = s.CategoryKey,
                    CategoryEntryCount = catCounts.GetValueOrDefault(s.CategoryId),
                    SubcategoryKey = s.SubCategoryKey,
                    SubcategoryEntryCount = subCounts.GetValueOrDefault(s.SubCategoryId),
                });
            }

            var tagIds = await this.db.DirectoryEntryTags.AsNoTracking()
                .Where(t => t.DirectoryEntryId == directoryEntryId)
                .Select(t => t.TagId)
                .ToListAsync(ct);
            if (tagIds.Count > 0)
            {
                var allTags = await this.tags.ListTagsWithSitemapInfoAsync();
                facts.TagCount = allTags.Count(t => t.EntryCount > 0);
                facts.Tags = allTags
                    .Where(t => tagIds.Contains(t.TagId))
                    .Select(t => new PagedPageFacts { Slug = t.Slug, EntryCount = t.EntryCount })
                    .ToList();
            }

            var code = (entry.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length > 0 && CountryHelper.GetCountries().ContainsKey(code))
            {
                var countries = await this.entries.GetActiveCountryCountsForSitemapAsync(ct);
                facts.CountryCount = countries.Count(c => c.Count > 0);
                facts.Country = new PagedPageFacts
                {
                    Slug = StringHelpers.UrlKey(CountryHelper.GetCountryName(code)),
                    EntryCount = countries
                        .Where(c => string.Equals((c.CountryCode ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase))
                        .Sum(c => c.Count),
                };
            }

            return facts;
        }
    }
}
