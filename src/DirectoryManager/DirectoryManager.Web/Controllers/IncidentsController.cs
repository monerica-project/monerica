using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models.Reviews;
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
    /// Public incidents page (/incidents), generated from existing data only. "Open issues" are the
    /// listings whose current status is Scam or Questionable (marking a listing so with a note opens
    /// an issue; setting it back to Admitted/Verified closes it). "Reported by reviewers" lists
    /// approved reviews with a problem tag. Only the plain page is indexed and mirrored.
    /// </summary>
    [AllowAnonymous]
    public class IncidentsController : BaseController
    {
        public const string PagePath = "incidents";
        private const string JsonCacheKey = "incidents.json";

        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache memoryCache;

        public IncidentsController(
            IMemoryCache cache,
            IDirectoryEntriesAuditRepository auditRepository,
            IDirectoryEntryReviewRepository reviewRepository,
            ICacheService cacheService)
            : base(cache)
        {
            this.auditRepository = auditRepository;
            this.reviewRepository = reviewRepository;
            this.cacheService = cacheService;
            this.memoryCache = cache;
        }

        /// <summary>
        /// Open issues as JSON (same rows as the page's "Open issues" list), for other sites and
        /// tools. Cached for 10 minutes; not in the sitemap (it is data, not a page).
        /// </summary>
        [HttpGet("incidents.json")]
        public async Task<IActionResult> Json(CancellationToken ct = default)
        {
            var json = await this.memoryCache.GetOrCreateAsync(JsonCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                var (audits, listings) = await this.auditRepository.GetOpenIssueHistoryAsync(ct);
                var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
                return OpenIssueJson.Build(OpenIssueBuilder.Build(audits, listings), canonicalDomain, DateTime.UtcNow);
            }) ?? "{}";

            this.Response.Headers["X-Robots-Tag"] = "noindex";
            return this.Content(json, "application/json", System.Text.Encoding.UTF8);
        }

        [HttpGet("incidents")]
        public async Task<IActionResult> Index(
            [FromQuery] string? kind,
            [FromQuery] string? days,
            [FromQuery] string? page,
            CancellationToken ct = default)
        {
            var filter = IncidentFilter.Parse(kind, days, page);
            int pageSize = filter.Kind == IncidentKind.All ? IncidentFilter.SectionPreviewSize : IncidentFilter.PageSize;

            // Open issues: every listing that is Scam or Questionable right now (no time window).
            List<OpenIssue> openAll = new ();
            if (filter.Kind != IncidentKind.Reviews)
            {
                var (audits, listings) = await this.auditRepository.GetOpenIssueHistoryAsync(ct);
                openAll = OpenIssueBuilder.Build(audits, listings);
            }

            // Reviewer reports: approved problem-tagged reviews in the chosen window (default 90 days).
            var reviewReports = filter.Kind == IncidentKind.Status
                ? new PagedResult<DirectoryEntryReview> { PageSize = pageSize, Page = 1 }
                : await this.reviewRepository.ListProblemReportsAsync(
                    filter.SinceUtc(DateTime.UtcNow),
                    filter.Kind == IncidentKind.Reviews ? filter.Page : 1,
                    pageSize,
                    ct);
            ReviewAuthorFlags.ApplyOwnerFlags(reviewReports.Items);

            int currentPage = 1;
            int totalPages = 1;
            List<OpenIssue> openItems;
            if (filter.Kind == IncidentKind.Status)
            {
                totalPages = PagingHelper.TotalPages(openAll.Count, pageSize);
                currentPage = Math.Clamp(filter.Page, 1, totalPages);
                openItems = openAll.Skip(PagingHelper.Skip(currentPage, pageSize)).Take(pageSize).ToList();
            }
            else
            {
                openItems = openAll.Take(pageSize).ToList();
                if (filter.Kind == IncidentKind.Reviews)
                {
                    totalPages = PagingHelper.TotalPages(reviewReports.TotalCount, pageSize);
                    currentPage = reviewReports.Page;
                }
            }

            var reviewIds = reviewReports.Items.Select(r => r.DirectoryEntryReviewId).ToList();
            var listingPages = reviewIds.Count == 0
                ? new Dictionary<int, int>()
                : await this.reviewRepository.GetApprovedReviewPageMapAsync(reviewIds, IntegerConstants.ReviewsPageSize, ct);

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

            return this.View("Index", new IncidentsViewModel
            {
                Filter = filter,
                OpenIssues = openItems,
                OpenIssueTotal = openAll.Count,
                ReviewReports = reviewReports,
                ListingPages = listingPages,
                Page = currentPage,
                TotalPages = totalPages,
            });
        }
    }
}
