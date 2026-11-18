using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.TrustPages;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Public list of approved reviews across all listings (/reviews), with GET filters. Only the
    /// plain first page is indexed and mirrored; filtered and paged variants are noindex and are
    /// served by the app (all filter and paging links point at the app domain).
    /// </summary>
    [AllowAnonymous]
    public class PublicReviewsController : BaseController
    {
        public const string PagePath = "reviews";

        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IReviewTagRepository reviewTagRepository;
        private readonly ICacheService cacheService;

        public PublicReviewsController(
            IMemoryCache cache,
            IDirectoryEntryReviewRepository reviewRepository,
            ICategoryRepository categoryRepository,
            IReviewTagRepository reviewTagRepository,
            ICacheService cacheService)
            : base(cache)
        {
            this.reviewRepository = reviewRepository;
            this.categoryRepository = categoryRepository;
            this.reviewTagRepository = reviewTagRepository;
            this.cacheService = cacheService;
        }

        [HttpGet("reviews")]
        public async Task<IActionResult> Index(
            [FromQuery] string? q,
            [FromQuery] string? category,
            [FromQuery] string? rating,
            [FromQuery] string? tag,
            [FromQuery] string? replies,
            [FromQuery] string? sort,
            [FromQuery] string? page,
            CancellationToken ct = default)
        {
            var categories = (await this.categoryRepository.GetActiveCategoriesAsync())
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var tags = (await this.reviewTagRepository.ListEnabledAsync(ct))
                .OrderBy(t => t.Level)
                .ThenBy(t => t.MinUsd ?? 0)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var filter = PublicReviewFilter.Parse(
                q,
                category,
                rating,
                tag,
                replies,
                sort,
                page,
                categories
                    .Where(c => !string.IsNullOrWhiteSpace(c.CategoryKey))
                    .GroupBy(c => c.CategoryKey.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().CategoryId),
                tags
                    .Where(t => !string.IsNullOrWhiteSpace(t.Slug))
                    .GroupBy(t => t.Slug.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().ReviewTagId));

            var result = await this.reviewRepository.ListPublicAsync(filter, ct);
            ReviewAuthorFlags.ApplyOwnerFlags(result.Items);

            var ids = result.Items.Select(r => r.DirectoryEntryReviewId).ToList();
            var replyCounts = await this.reviewRepository.GetApprovedReplyCountsAsync(ids, ct);
            var listingPages = ids.Count == 0
                ? new Dictionary<int, int>()
                : await this.reviewRepository.GetApprovedReviewPageMapAsync(ids, IntegerConstants.ReviewsPageSize, ct);

            var meta = this.PageMeta();
            if (filter.IsDefault)
            {
                var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
                meta.CanonicalUrl = UrlBuilder.CombineUrl(canonicalDomain, PagePath);
            }
            else
            {
                meta.IsIndexable = false;
                meta.CanonicalUrl = null;
            }

            return this.View("Index", new PublicReviewsViewModel
            {
                Filter = filter.WithPage(result.Page),
                Result = result,
                Categories = categories,
                Tags = tags,
                ReplyCounts = replyCounts,
                ListingPages = listingPages,
            });
        }
    }
}
