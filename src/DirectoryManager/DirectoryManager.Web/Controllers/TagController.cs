using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Route("tagged")]
    public class TagController : Controller
    {
        private const int PageSize = Constants.IntegerConstants.MaxPageSize;

        private readonly ITagRepository tagRepo;
        private readonly IDirectoryEntryTagRepository entryTagRepo;
        private readonly ICacheService cacheService;

        // ✅ add these
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IDirectoryEntryReviewRepository entryReviewsRepo;
        private readonly IMemoryCache cache;

        public TagController(
            ITagRepository tagRepo,
            IDirectoryEntryTagRepository entryTagRepo,
            ICacheService cacheService,
            ISponsoredListingRepository sponsoredListingRepository,
            IDirectoryEntryReviewRepository entryReviewsRepo,
            IMemoryCache cache)
        {
            this.tagRepo = tagRepo ?? throw new ArgumentNullException(nameof(tagRepo));
            this.entryTagRepo = entryTagRepo ?? throw new ArgumentNullException(nameof(entryTagRepo));
            this.cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

            this.sponsoredListingRepository = sponsoredListingRepository ?? throw new ArgumentNullException(nameof(sponsoredListingRepository));
            this.entryReviewsRepo = entryReviewsRepo ?? throw new ArgumentNullException(nameof(entryReviewsRepo));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [AllowAnonymous]
        [HttpGet("")]
        [HttpGet("page/{page:int}")]
        public async Task<IActionResult> All(int page = 1)
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            var path = page > 1 ? $"tagged/page/{page}" : "tagged";

            this.ViewData[StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(canonicalDomain, path);

            var pageSize = Constants.IntegerConstants.MaxPageSize;
            var paged = await this.tagRepo
                .ListTagsWithCountsPagedAsync(page, pageSize)
                .ConfigureAwait(false);

            var vm = new TagListViewModel
            {
                PagedTags = paged,
                CurrentPage = page,
                PageSize = pageSize
            };

            return this.View("AllTags", vm);
        }

        [AllowAnonymous]
        [HttpGet("{tagSlug}")]
        [HttpGet("{tagSlug}/page/{page:int}")]
        public async Task<IActionResult> Index(string tagSlug, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(tagSlug))
            {
                return this.NotFound();
            }

            var tag = await this.tagRepo.GetByKeyAsync(tagSlug);
            if (tag == null)
            {
                return this.NotFound();
            }

            await this.SetCanonicalAsync(tagSlug, page);

            // get the paged raw entries
            var paged = await this.entryTagRepo.ListEntriesForTagPagedAsync(
                tag.Name,
                page,
                PageSize);

            // grab link-2/3 names just once
            var link2 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3 = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var items = ViewModelConverter.ConvertToViewModels(
                paged.Items.ToList(),
                DateDisplayOption.NotDisplayed,
                ItemDisplayType.Normal,
                link2,
                link3);

            // ✅ ensure tag lists behave like other directory lists
            foreach (var vm in items)
            {
                vm.LinkType = LinkType.ListingPage;
            }

            // ✅ sponsor highlight ids (cached)
            var sponsoredIds = await this.GetAllSponsoredEntryIdsAsync();

            // ✅ apply sponsor flags
            ApplySponsorFlags(items, sponsoredIds);

            // ✅ apply ratings (avg + count)
            await this.ApplyRatingsAsync(items);

            var vmOut = new TaggedEntriesViewModel
            {
                Tag = tag,
                PagedEntries = new PagedResult<DirectoryEntryViewModel>
                {
                    Items = items,
                    TotalCount = paged.TotalCount
                },
                CurrentPage = page,
                PageSize = PageSize
            };

            return this.View(vmOut);
        }

        // -------------------------
        // Helpers
        // -------------------------

        private async Task SetCanonicalAsync(string tagSlug, int page)
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            var basePath = $"tagged/{tagSlug}";
            var path = page > 1 ? $"{basePath}/page/{page}" : basePath;

            this.ViewData[StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(canonicalDomain, path);
        }

        private async Task<HashSet<int>> GetAllSponsoredEntryIdsAsync()
        {
            // Cache so Tag pages don’t hammer the DB
            const string cacheKey = StringConstants.CacheKeyAllActiveSponsors;

            var allSponsors = await this.cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var sponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
                return sponsors?.ToList() ?? new List<DirectoryManager.Data.Models.SponsoredListings.SponsoredListing>();
            }) ?? new List<DirectoryManager.Data.Models.SponsoredListings.SponsoredListing>();

            return allSponsors
                .Select(s => s.DirectoryEntryId)
                .Distinct()
                .ToHashSet();
        }

        private static void ApplySponsorFlags(IReadOnlyList<DirectoryEntryViewModel> items, HashSet<int> sponsoredIds)
        {
            foreach (var item in items)
            {
                if (sponsoredIds.Contains(item.DirectoryEntryId))
                {
                    item.IsSponsored = true;
                    item.DisplayAsSponsoredItem = true;
                }
            }
        }

        private async Task ApplyRatingsAsync(IReadOnlyList<DirectoryEntryViewModel> items)
        {
            var ids = items.Select(x => x.DirectoryEntryId).Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var ratingMap = await this.entryReviewsRepo.GetRatingSummariesAsync(ids);

            foreach (var item in items)
            {
                if (ratingMap.TryGetValue(item.DirectoryEntryId, out var rs) && rs.ReviewCount > 0)
                {
                    item.AverageRating = rs.AvgRating;
                    item.ReviewCount = rs.ReviewCount;
                }
                else
                {
                    item.AverageRating = null;
                    item.ReviewCount = 0;
                }
            }
        }
    }
}
