using System.Net;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Reviews;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DirectoryEntry = DirectoryManager.Data.Models.DirectoryEntry;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class DirectoryEntryController : BaseController
    {
        // -------------------------------------------------------------------------
        // Fields
        // -------------------------------------------------------------------------

        private readonly UserManager<ApplicationUser> userManager;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly ITagRepository tagRepo;
        private readonly IDirectoryEntryTagRepository entryTagRepo;
        private readonly ICacheService cacheService;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IMemoryCache cache;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly ISubmissionRepository submissionRepository;
        private readonly IUrlResolutionService urlResolver;
        private readonly IDirectoryEntryReviewCommentRepository reviewCommentRepository;
        private readonly IAdditionalLinkRepository additionalLinkRepo;

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------

        public DirectoryEntryController(
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository entryRepository,
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntriesAuditRepository auditRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            ITagRepository tagRepo,
            IDirectoryEntryTagRepository entryTagRepo,
            ICacheService cacheService,
            ISponsoredListingRepository sponsoredListingRepository,
            IMemoryCache cache,
            IDirectoryEntryReviewRepository reviewRepository,
            ISubmissionRepository submissionRepository,
            IUrlResolutionService urlResolver,
            IDirectoryEntryReviewCommentRepository reviewCommentRepository,
            IAdditionalLinkRepository additionalLinkRepo)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.directoryEntryRepository = entryRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.auditRepository = auditRepository;
            this.tagRepo = tagRepo;
            this.entryTagRepo = entryTagRepo;
            this.cache = cache;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.cacheService = cacheService;
            this.reviewRepository = reviewRepository;
            this.submissionRepository = submissionRepository;
            this.urlResolver = urlResolver;
            this.reviewCommentRepository = reviewCommentRepository;
            this.additionalLinkRepo = additionalLinkRepo;
        }

        // -------------------------------------------------------------------------
        // Public Actions
        // -------------------------------------------------------------------------

        [Route("directoryentry/index")]
        public async Task<IActionResult> Index(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries
                    .Where(e => e.SubCategory != null && e.SubCategory.SubCategoryId == subCategoryId.Value)
                    .ToList();
            }

            entries = entries.OrderBy(e => e.Name).ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllDtoAsync())
                .OrderBy(sc => sc.CategoryName)
                .ThenBy(sc => sc.Name)
                .ToList();

            return this.View(entries);
        }

        [HttpGet]
        [Route("directoryentry/create")]
        public async Task<IActionResult> Create()
        {
            await this.LoadLists();
            await this.LoadTagCheckboxesAsync(new HashSet<int>());

            var vm = new DirectoryEntryEditViewModel
            {
                DirectoryStatus = DirectoryStatus.Unknown,
                SubCategoryId = 0,
                SelectedTagIds = new List<int>()
            };

            return this.View(vm);
        }

        [HttpPost]
        [Route("directoryentry/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DirectoryEntryEditViewModel vm)
        {
            var normalizedAdditional = NormalizeAdditionalLinks(vm.AdditionalLinks, IntegerConstants.MaxAdditionalLinks);

            for (int i = 0; i < normalizedAdditional.Count; i++)
            {
                if (!UrlHelper.IsValidUrl(normalizedAdditional[i]))
                {
                    this.ModelState.AddModelError(string.Empty, $"Additional link {i + 1} is not a valid URL.");
                }
            }

            if (!TryParseFoundedDate(vm, out var foundedDate, out var foundedErr))
            {
                this.ModelState.AddModelError(nameof(vm.FoundedYear), foundedErr!);
            }

            if (!this.ModelState.IsValid || vm.DirectoryStatus == DirectoryStatus.Unknown || vm.SubCategoryId == 0)
            {
                await this.LoadLists();
                await this.LoadTagCheckboxesAsync(NormalizeSelectedIds(vm.SelectedTagIds));
                vm.AdditionalLinks = normalizedAdditional;
                return this.View("create", vm);
            }

            var rawLink = vm.Link?.Trim() ?? string.Empty;
            var linkWithoutSlash = rawLink.TrimEnd('/');
            var linkWithSlash = linkWithoutSlash + "/";

            var existingEntryByLink =
                await this.directoryEntryRepository.GetByLinkAsync(linkWithoutSlash) ??
                await this.directoryEntryRepository.GetByLinkAsync(linkWithSlash);

            if (existingEntryByLink != null)
            {
                await this.LoadLists();
                await this.LoadTagCheckboxesAsync(NormalizeSelectedIds(vm.SelectedTagIds));
                vm.AdditionalLinks = normalizedAdditional;
                this.ModelState.AddModelError("Link", "The provided link is already used by another entry (with or without a trailing slash).");
                return this.View("create", vm);
            }

            var entryName = (vm.Name ?? string.Empty).Trim();
            var existingEntryByName = await this.directoryEntryRepository.GetByNameAsync(entryName);

            if (existingEntryByName != null)
            {
                await this.LoadLists();
                await this.LoadTagCheckboxesAsync(NormalizeSelectedIds(vm.SelectedTagIds));
                vm.AdditionalLinks = normalizedAdditional;
                this.ModelState.AddModelError("Name", "The provided name is already used by another entry.");
                return this.View("create", vm);
            }

            var model = new DirectoryEntry
            {
                CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty,
                SubCategoryId = vm.SubCategoryId,
                DirectoryStatus = vm.DirectoryStatus,
                Link = linkWithoutSlash,
                Name = entryName,
                DirectoryEntryKey = StringHelpers.UrlKey(entryName),
                Description = vm.Description?.Trim(),
                Note = vm.Note?.Trim(),
                Contact = vm.Contact?.Trim(),
                Location = vm.Location?.Trim(),
                Processor = vm.Processor?.Trim(),
                LinkA = vm.LinkA?.Trim(),
                Link2 = vm.Link2?.Trim(),
                Link2A = vm.Link2A?.Trim(),
                Link3 = vm.Link3?.Trim(),
                Link3A = vm.Link3A?.Trim(),
                PgpKey = vm.PgpKey?.Trim(),
                ProofLink = vm.ProofLink?.Trim(),
                VideoLink = vm.VideoLink?.Trim(),
                CountryCode = vm.CountryCode,
                FoundedDate = foundedDate
            };

            await this.directoryEntryRepository.CreateAsync(model);
            await this.SyncAdditionalLinksAsync(model.DirectoryEntryId, normalizedAdditional);
            await this.AssignTagsAsync(model.DirectoryEntryId, NormalizeSelectedIds(vm.SelectedTagIds), vm.NewTagsCsv);

            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        [Route("directoryentry/edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (entry == null)
            {
                return this.NotFound();
            }

            await this.LoadSubCategories();
            await this.PopulateCountryDropDownList(entry.CountryCode);

            var currentTags = await this.entryTagRepo.GetTagsForEntryAsync(id);
            var selectedIds = currentTags.Select(t => t.TagId).ToHashSet();

            await this.LoadTagCheckboxesAsync(selectedIds);

            var additional = await this.additionalLinkRepo.GetByDirectoryEntryIdAsync(id, CancellationToken.None);

            var additionalLinks = (additional ?? new List<AdditionalLink>())
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.AdditionalLinkId)
                .Select(x => x.Link)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(IntegerConstants.MaxAdditionalLinks)
                .ToList();

            while (additionalLinks.Count < IntegerConstants.MaxAdditionalLinks)
            {
                additionalLinks.Add(string.Empty);
            }

            var vm = new DirectoryEntryEditViewModel
            {
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryStatus = entry.DirectoryStatus,
                SubCategoryId = entry.SubCategoryId,
                Name = entry.Name,
                Link = entry.Link,
                LinkA = entry.LinkA,
                Link2 = entry.Link2,
                Link2A = entry.Link2A,
                Link3 = entry.Link3,
                Link3A = entry.Link3A,
                ProofLink = entry.ProofLink,
                VideoLink = entry.VideoLink,
                Location = entry.Location,
                CountryCode = entry.CountryCode,
                Processor = entry.Processor,
                Contact = entry.Contact,
                Description = entry.Description,
                Note = entry.Note,
                PgpKey = entry.PgpKey,
                FoundedYear = entry.FoundedDate?.Year.ToString("0000"),
                FoundedMonth = entry.FoundedDate?.Month.ToString("00"),
                FoundedDay = entry.FoundedDate?.Day.ToString("00"),
                SelectedTagIds = selectedIds.ToList(),
                AdditionalLinks = additionalLinks
            };

            return this.View(vm);
        }

        [HttpPost]
        [Route("directoryentry/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DirectoryEntryEditViewModel vm)
        {
            var existingEntry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (existingEntry == null)
            {
                return this.NotFound();
            }

            var normalizedAdditional = NormalizeAdditionalLinks(vm.AdditionalLinks, IntegerConstants.MaxAdditionalLinks);

            for (int i = 0; i < normalizedAdditional.Count; i++)
            {
                if (!UrlHelper.IsValidUrl(normalizedAdditional[i]))
                {
                    this.ModelState.AddModelError(string.Empty, $"Additional link {i + 1} is not a valid URL.");
                }
            }

            if (!TryParseFoundedDate(vm, out var foundedDate, out var foundedErr))
            {
                this.ModelState.AddModelError(nameof(vm.FoundedYear), foundedErr!);
            }

            if (!this.ModelState.IsValid || vm.DirectoryStatus == DirectoryStatus.Unknown || vm.SubCategoryId == 0)
            {
                await this.LoadSubCategories();
                await this.PopulateCountryDropDownList(vm.CountryCode);
                await this.LoadTagCheckboxesAsync(NormalizeSelectedIds(vm.SelectedTagIds));
                vm.AdditionalLinks = normalizedAdditional;
                return this.View(vm);
            }

            existingEntry.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingEntry.SubCategoryId = vm.SubCategoryId;
            existingEntry.Link = (vm.Link ?? string.Empty).Trim();
            existingEntry.LinkA = vm.LinkA?.Trim();
            existingEntry.Link2 = vm.Link2?.Trim();
            existingEntry.Link2A = vm.Link2A?.Trim();
            existingEntry.Link3 = vm.Link3?.Trim();
            existingEntry.Link3A = vm.Link3A?.Trim();
            existingEntry.ProofLink = vm.ProofLink?.Trim();
            existingEntry.VideoLink = vm.VideoLink?.Trim();
            existingEntry.Name = (vm.Name ?? string.Empty).Trim();
            existingEntry.DirectoryEntryKey = StringHelpers.UrlKey(existingEntry.Name);
            existingEntry.Description = vm.Description?.Trim();
            existingEntry.Note = vm.Note?.Trim();
            existingEntry.DirectoryStatus = vm.DirectoryStatus;
            existingEntry.Contact = vm.Contact?.Trim();
            existingEntry.Location = vm.Location?.Trim();
            existingEntry.Processor = vm.Processor?.Trim();
            existingEntry.CountryCode = vm.CountryCode;
            existingEntry.PgpKey = vm.PgpKey?.Trim();
            existingEntry.FoundedDate = foundedDate;

            await this.directoryEntryRepository.UpdateAsync(existingEntry);
            await this.SyncAdditionalLinksAsync(id, normalizedAdditional);
            await this.SyncTagsAsync(id, NormalizeSelectedIds(vm.SelectedTagIds), vm.NewTagsCsv);

            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        [Route("directoryentry/entryaudits/{entryId}")]
        public async Task<IActionResult> EntryAudits(int entryId)
        {
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(entryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            var audits = await this.auditRepository.GetAuditsWithSubCategoriesForEntryAsync(entryId);
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel
            {
                CreateDate = directoryEntry.CreateDate,
                UpdateDate = directoryEntry.UpdateDate,
                DateOption = DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = directoryEntry.Link,
                Name = directoryEntry.Name,
                DirectoryEntryKey = directoryEntry.DirectoryEntryKey,
                Contact = directoryEntry.Contact,
                Description = directoryEntry.Description,
                DirectoryEntryId = directoryEntry.DirectoryEntryId,
                DirectoryStatus = directoryEntry.DirectoryStatus,
                Link2 = directoryEntry.Link2,
                Link3 = directoryEntry.Link3,
                Location = directoryEntry.Location,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
                CountryCode = directoryEntry.CountryCode,
                PgpKey = directoryEntry.PgpKey,
                ProofLink = directoryEntry.ProofLink,
                VideoLink = directoryEntry.VideoLink,
            };

            foreach (var audit in audits)
            {
                audit.SubCategoryName = audit.SubCategory != null
                    ? $"{audit.SubCategory.Category?.Name} > {audit.SubCategory.Name}"
                    : "No SubCategory Assigned";
            }

            return this.View(audits);
        }

        [HttpGet("directoryentry/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.directoryEntryRepository.DeleteAsync(id);
            this.ClearCachedItems();
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("directoryentry/report")]
        public IActionResult Report()
        {
            return this.View();
        }

        [HttpGet("directoryentry/submissionmonthlyplotimage")]
        public async Task<IActionResult> SubmissionMonthlyPlotImage()
        {
            var submissions = await this.submissionRepository.GetAllAsync();
            var plotting = new SubmissionsPlotting();
            var bytes = plotting.CreateMonthlySubmissionBarChart(submissions);

            return this.File(
                bytes.Length == 0 ? Array.Empty<byte>() : bytes,
                StringConstants.PngImage);
        }

        [HttpGet("directoryentry/monthlyplotimage")]
        public async Task<IActionResult> MonthlyPlotImageAsync()
        {
            var entries = await this.auditRepository.GetAllAsync();
            var imageBytes = new DirectoryEntryPlotting().CreateMonthlyActivePlot(entries.ToList());
            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/categorypiechart")]
        public async Task<IActionResult> CategoryPieChartImageAsync()
        {
            var allCategories = await this.categoryRepository.GetActiveCategoriesAsync();
            var entries = await this.directoryEntryRepository.GetAllActiveEntries();
            var imageBytes = new DirectoryEntryPlotting().CreateCategoryPieChartImage(entries, allCategories);
            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/countrieschart")]
        public async Task<IActionResult> CountryPlotImageAsync()
        {
            var entries = await this.directoryEntryRepository.GetAllActiveEntries();
            var knownCountries = CountryHelper.GetCountries();

            var filtered = (entries ?? Enumerable.Empty<DirectoryEntry>())
                .Where(e => !string.IsNullOrWhiteSpace(e.CountryCode)
                         && knownCountries.ContainsKey(e.CountryCode!.Trim().ToUpperInvariant()))
                .ToList();

            var imageBytes = new DirectoryEntryPlotting().CreateActiveCountriesPieChartImage(filtered);

            return this.File(
                imageBytes.Length == 0 ? Array.Empty<byte>() : imageBytes,
                StringConstants.PngImage);
        }

        [HttpGet("directoryentry/subcategory-trends")]
        public async Task<IActionResult> SubcategoryTrends([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            const string unknown = "(Unknown)";
            const int UnknownSubCatKey = -1;

            DateTime to = end ?? DateTime.UtcNow;
            DateTime from = start ?? to.AddYears(-1);

            var audits = await this.auditRepository.GetAllWithSubcategoriesAsync(from.AddYears(-5), to);

            var vmEmpty = new SubcategoryTrendsReportViewModel { Start = from, End = to };
            if (audits.Count == 0)
            {
                return this.View("SubcategoryTrends", vmEmpty);
            }

            var byEntry = audits
                .Select(a => new TimelineItem
                {
                    DirectoryEntryId = a.DirectoryEntryId,
                    Effective = a.UpdateDate ?? a.CreateDate,
                    DirectoryStatus = a.DirectoryStatus,
                    SubCategoryId = a.SubCategoryId,
                    SubCategory = a.SubCategory
                })
                .GroupBy(x => x.DirectoryEntryId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Effective).ToList());

            static bool IsActive(DirectoryManager.Data.Enums.DirectoryStatus s) =>
                s == DirectoryManager.Data.Enums.DirectoryStatus.Admitted
                || s == DirectoryManager.Data.Enums.DirectoryStatus.Verified
                || s == DirectoryManager.Data.Enums.DirectoryStatus.Questionable
                || s == DirectoryManager.Data.Enums.DirectoryStatus.Scam;

            (int? subCatId, string name, bool active) StateAt(DateTime t, List<TimelineItem> timeline)
            {
                var rec = timeline.LastOrDefault(r => r.Effective <= t);
                if (rec is null)
                {
                    return (null, unknown, false);
                }

                string scName = rec.SubCategory is null
                    ? unknown
                    : $"{rec.SubCategory.Category?.Name ?? unknown} > {rec.SubCategory.Name ?? unknown}";

                return (rec.SubCategoryId, scName, IsActive(rec.DirectoryStatus));
            }

            var startCounts = new Dictionary<int, (string name, int count)>();
            var endCounts = new Dictionary<int, (string name, int count)>();

            foreach (var kvp in byEntry)
            {
                var timeline = kvp.Value;

                var (sid, sname, sactive) = StateAt(from, timeline);
                if (sactive)
                {
                    var sKey = sid ?? UnknownSubCatKey;
                    startCounts[sKey] = startCounts.TryGetValue(sKey, out var s)
                        ? (s.name, s.count + 1)
                        : (sname, 1);
                }

                var (eid, ename, eactive) = StateAt(to, timeline);
                if (eactive)
                {
                    var eKey = eid ?? UnknownSubCatKey;
                    endCounts[eKey] = endCounts.TryGetValue(eKey, out var e)
                        ? (e.name, e.count + 1)
                        : (ename, 1);
                }
            }

            var allIds = new HashSet<int>(startCounts.Keys.Concat(endCounts.Keys));

            var trends = allIds.Select(key =>
            {
                var s = startCounts.TryGetValue(key, out var sv) ? sv : (name: unknown, count: 0);
                var e = endCounts.TryGetValue(key, out var ev) ? ev : (name: s.name, count: 0);

                return new SubcategoryTrendItem
                {
                    SubCategoryId = key == UnknownSubCatKey ? (int?)null : key,
                    SubCategoryName = string.IsNullOrWhiteSpace(e.name) ? s.name : e.name,
                    StartCount = s.count,
                    EndCount = e.count
                };
            }).ToList();

            const int countToTake = 100;

            var vm = new SubcategoryTrendsReportViewModel
            {
                Start = from,
                End = to,
                TopGrowth = trends
                    .OrderByDescending(t => t.PercentChange)
                    .ThenByDescending(t => t.Delta)
                    .Take(countToTake)
                    .ToList(),
                TopDecline = trends
                    .OrderBy(t => t.PercentChange)
                    .ThenBy(t => t.Delta)
                    .Take(countToTake)
                    .ToList()
            };

            return this.View("SubcategoryTrends", vm);
        }

        [AllowAnonymous]
        [HttpGet("site/{directoryEntryKey}")]
        [HttpGet("site/{directoryEntryKey}/page/{page:int}")]
        public async Task<IActionResult> DirectoryEntryView(
            string directoryEntryKey,
            int page = 1,
            CancellationToken ct = default)
        {
            int requestedPage = page < 1 ? 1 : page;

            if (requestedPage == 1 && this.RouteData.Values.ContainsKey("page"))
            {
                return this.RedirectPermanent($"/site/{directoryEntryKey}");
            }

            var entry = await this.GetEntryOr404Async(directoryEntryKey);
            if (entry == null)
            {
                return this.NotFound();
            }

            this.SetMetaDescription(entry);

            var (link2Name, link3Name) = await this.GetLinkLabelsAsync();
            var (tagNames, tagDict) = await this.GetTagsAsync(entry.DirectoryEntryId);
            var additionalLinkUrls = await this.GetAdditionalLinkUrlsAsync(entry.DirectoryEntryId, ct);
            bool isSponsor = await this.IsEntrySponsoredAsync(entry.DirectoryEntryId);

            var (reviewsVm, effectivePage) = await this.BuildReviewsVmAsync(entry, requestedPage, ct);

            if (effectivePage != requestedPage)
            {
                return effectivePage == 1
                    ? this.RedirectPermanent($"/site/{directoryEntryKey}")
                    : this.RedirectPermanent($"/site/{directoryEntryKey}/page/{effectivePage}");
            }

            await this.SetCanonicalAsync(directoryEntryKey, effectivePage);

            this.ViewBag.ReviewsVm = reviewsVm;

            var model = this.BuildDirectoryEntryViewModel(
                entry, link2Name, link3Name, tagNames, tagDict, isSponsor, additionalLinkUrls);

            await this.SetCategoryContextViewBagAsync(entry.SubCategoryId);

            return this.View("DirectoryEntryView", model);
        }

        // -------------------------------------------------------------------------
        // Private Static Methods
        // -------------------------------------------------------------------------

        private static HashSet<int> NormalizeSelectedIds(IEnumerable<int>? ids)
        {
            return (ids ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();
        }

        private static List<string> NormalizeAdditionalLinks(
            IEnumerable<string>? links,
            int max = IntegerConstants.MaxAdditionalLinks)
        {
            return (links ?? Enumerable.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        private static bool TryParseFoundedDate(
            DirectoryEntryEditViewModel vm,
            out DateOnly? foundedDate,
            out string? error)
        {
            foundedDate = null;
            error = null;

            var yRaw = (vm.FoundedYear ?? string.Empty).Trim();
            var mRaw = (vm.FoundedMonth ?? string.Empty).Trim();
            var dRaw = (vm.FoundedDay ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(yRaw) &&
                string.IsNullOrWhiteSpace(mRaw) &&
                string.IsNullOrWhiteSpace(dRaw))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(yRaw) ||
                string.IsNullOrWhiteSpace(mRaw) ||
                string.IsNullOrWhiteSpace(dRaw))
            {
                error = "FoundedDate requires Year, Month, and Day (YYYY MM DD).";
                return false;
            }

            if (!int.TryParse(yRaw, out var y) ||
                !int.TryParse(mRaw, out var m) ||
                !int.TryParse(dRaw, out var d))
            {
                error = "FoundedDate must be numeric (YYYY MM DD).";
                return false;
            }

            if (y < 1000 || y > DateTime.UtcNow.Year + 1)
            {
                error = "Founded year looks invalid.";
                return false;
            }

            try
            {
                foundedDate = new DateOnly(y, m, d);
                return true;
            }
            catch
            {
                error = "FoundedDate is not a real calendar date.";
                return false;
            }
        }

        private static string BuildLocationHtml(
            string? locationRaw,
            string? countryCode,
            IUrlResolutionService urlResolver)
        {
            var location = locationRaw?.Trim();
            var ccRaw = countryCode?.Trim();

            string flagHtml = string.Empty;
            if (!string.IsNullOrWhiteSpace(ccRaw))
            {
                var ccLower = ccRaw.ToLowerInvariant();
                var countryNameForAlt = CountryHelper.GetCountryName(ccRaw) ?? string.Empty;
                var altTitle = string.IsNullOrWhiteSpace(countryNameForAlt)
                    ? $"Flag ({ccRaw})"
                    : $"Flag of {countryNameForAlt} ({ccRaw.ToUpperInvariant()})";

                flagHtml =
                    $"<img class=\"country-flag me-2 align-text-bottom\" " +
                    $"src=\"/images/flags/{ccLower}.png\" " +
                    $"alt=\"{WebUtility.HtmlEncode(altTitle)}\" " +
                    $"title=\"{WebUtility.HtmlEncode(countryNameForAlt)}\" /> ";
            }

            string? countryName = null;
            string? anchorHtml = null;

            if (!string.IsNullOrWhiteSpace(ccRaw))
            {
                countryName = CountryHelper.GetCountryName(ccRaw);
                if (!string.IsNullOrWhiteSpace(countryName))
                {
                    var slug = StringHelpers.UrlKey(countryName);
                    var href = urlResolver.ResolveToRoot($"/countries/{slug}");
                    anchorHtml = $"<a class=\"no-app-link\" href=\"{href}\">{WebUtility.HtmlEncode(countryName)}</a>";
                }
            }

            if (string.IsNullOrWhiteSpace(location) && string.IsNullOrWhiteSpace(countryName))
            {
                return flagHtml;
            }

            if (string.IsNullOrWhiteSpace(countryName) || string.IsNullOrWhiteSpace(anchorHtml))
            {
                return $"{flagHtml}{WebUtility.HtmlEncode(location ?? string.Empty)}";
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var pattern = $@"(?i)(?<sep>,\s*|\s*)\b{Regex.Escape(countryName!)}\b\s*$";
                var match = Regex.Match(location!, pattern);

                if (match.Success)
                {
                    var before = location!.Substring(0, match.Index);
                    var sep = match.Groups["sep"].Value;
                    return $"{flagHtml}{WebUtility.HtmlEncode(before)}{WebUtility.HtmlEncode(sep)}{anchorHtml}";
                }

                var left = location!.TrimEnd();
                left += left.EndsWith(",", StringComparison.Ordinal) ? " " : ", ";

                return $"{flagHtml}{WebUtility.HtmlEncode(left)}{anchorHtml}";
            }

            return $"{flagHtml}{anchorHtml}";
        }

        // -------------------------------------------------------------------------
        // Private Instance Methods
        // -------------------------------------------------------------------------

        private async Task LoadLists()
        {
            await this.LoadSubCategories();
            await this.PopulateCountryDropDownList();
        }

        private async Task LoadSubCategories()
        {
            var subCategories = (await this.subCategoryRepository.GetAllDtoAsync())
                .OrderBy(sc => sc.CategoryName)
                .ThenBy(sc => sc.Name)
                .Select(sc => new
                {
                    sc.SubcategoryId,
                    DisplayName = $"{sc.CategoryName} > {sc.Name}"
                })
                .ToList();

            subCategories.Insert(0, new { SubcategoryId = 0, DisplayName = Constants.StringConstants.SelectACategory });

            this.ViewBag.SubCategories = subCategories;
        }

        private async Task PopulateCountryDropDownList(object? selectedId = null)
        {
            var countries = CountryHelper.GetCountries()
                .OrderBy(x => x.Value)
                .ToDictionary<string, string>();

            var list = countries
                .Select(c => new SelectListItem { Value = c.Key, Text = c.Value })
                .ToList();

            list.Insert(0, new SelectListItem { Value = "", Text = StringConstants.SelectText });

            this.ViewBag.CountryCode = new SelectList(list, "Value", "Text", selectedId);

            await Task.CompletedTask;
        }

        private async Task LoadTagCheckboxesAsync(HashSet<int>? selectedIds = null)
        {
            var allTags = await this.tagRepo.ListAllAsync();

            this.ViewBag.AllTags = allTags
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new TagOptionVm { TagId = t.TagId, Name = t.Name })
                .ToList();

            this.ViewBag.SelectedTagIds = selectedIds ?? new HashSet<int>();
        }

        private async Task SyncAdditionalLinksAsync(
            int directoryEntryId,
            List<string> normalizedLinks,
            CancellationToken ct = default)
        {
            await this.additionalLinkRepo.DeleteByDirectoryEntryIdAsync(directoryEntryId, ct);

            for (int i = 0; i < normalizedLinks.Count; i++)
            {
                await this.additionalLinkRepo.CreateAsync(
                    new AdditionalLink
                    {
                        DirectoryEntryId = directoryEntryId,
                        Link = normalizedLinks[i],
                        SortOrder = i + 1
                    },
                    ct);
            }
        }

        private async Task AssignTagsAsync(int entryId, HashSet<int> selectedIds, string? newTagsCsv)
        {
            foreach (var tagId in selectedIds)
            {
                await this.entryTagRepo.AssignTagAsync(entryId, tagId);
            }

            await this.CreateAndAssignNewTagsAsync(entryId, newTagsCsv);
        }

        private async Task SyncTagsAsync(int entryId, HashSet<int> newSelected, string? newTagsCsv)
        {
            var current = await this.entryTagRepo.GetTagsForEntryAsync(entryId);
            var currentIds = current.Select(t => t.TagId).ToHashSet();

            foreach (var tagId in currentIds.Where(x => !newSelected.Contains(x)))
            {
                await this.entryTagRepo.RemoveTagAsync(entryId, tagId);
            }

            foreach (var tagId in newSelected.Where(x => !currentIds.Contains(x)))
            {
                await this.entryTagRepo.AssignTagAsync(entryId, tagId);
            }

            await this.CreateAndAssignNewTagsAsync(entryId, newTagsCsv);
        }

        private async Task CreateAndAssignNewTagsAsync(int entryId, string? newTagsCsv)
        {
            if (string.IsNullOrWhiteSpace(newTagsCsv))
            {
                return;
            }

            var newNames = newTagsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in newNames)
            {
                var normalizedName = FormattingHelper.NormalizeTagName(name);
                var tag = await this.tagRepo.GetByKeyAsync(normalizedName.UrlKey())
                          ?? await this.tagRepo.CreateAsync(normalizedName);

                await this.entryTagRepo.AssignTagAsync(entryId, tag.TagId);
            }
        }

        private async Task<List<string>> GetAdditionalLinkUrlsAsync(int directoryEntryId, CancellationToken ct)
        {
            var additionalLinks = await this.additionalLinkRepo.GetByDirectoryEntryIdAsync(directoryEntryId, ct);

            return (additionalLinks ?? new List<AdditionalLink>())
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Link)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<bool> IsEntrySponsoredAsync(int directoryEntryId)
        {
            var sponsors = await this.GetAllSponsorsCachedAsync();
            return sponsors.Any(s => s.DirectoryEntryId == directoryEntryId);
        }

        private async Task<List<SponsoredListing>> GetAllSponsorsCachedAsync()
        {
            return await this.cache.GetOrCreateAsync(
                StringConstants.CacheKeyAllActiveSponsors,
                async cacheEntry =>
                {
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                    var sponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
                    return sponsors?.ToList() ?? new List<SponsoredListing>();
                }) ?? new List<SponsoredListing>();
        }

        private async Task<IReadOnlyDictionary<string, int>> GetAuthorPostCountsCachedAsync(CancellationToken ct)
        {
            return await this.cache.GetOrCreateAsync(
                StringConstants.CacheKeyAuthorPostCounts,
                async cacheEntry =>
                {
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                    var reviewCounts = await this.reviewRepository.GetApprovedReviewCountsByAuthorAsync(ct);
                    var replyCounts = await this.reviewCommentRepository.GetApprovedReplyCountsByAuthorAsync(ct);

                    var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var kv in reviewCounts.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)))
                    {
                        merged[kv.Key] = kv.Value;
                    }

                    foreach (var kv in replyCounts.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)))
                    {
                        merged[kv.Key] = merged.TryGetValue(kv.Key, out var cur)
                            ? cur + kv.Value
                            : kv.Value;
                    }

                    return (IReadOnlyDictionary<string, int>)merged;
                }) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<DirectoryEntry?> GetEntryOr404Async(string directoryEntryKey)
        {
            var entry = await this.directoryEntryRepository.GetByKey(directoryEntryKey);

            return entry == null || entry.DirectoryStatus == DirectoryStatus.Removed
                ? null
                : entry;
        }

        private async Task<(string link2Name, string link3Name)> GetLinkLabelsAsync()
        {
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);
            return (link2Name, link3Name);
        }

        private async Task<(List<string> tagNames, Dictionary<string, string> tagDictionary)> GetTagsAsync(
            int directoryEntryId)
        {
            var entryTagIds = (await this.entryTagRepo.GetTagsForEntryAsync(directoryEntryId))
                .Select(t => t.TagId)
                .Distinct()
                .ToHashSet();

            if (entryTagIds.Count == 0)
            {
                return (new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            var allTags = await this.tagRepo.ListAllAsync();
            var tagEntities = allTags.Where(t => entryTagIds.Contains(t.TagId)).ToList();

            var tagNames = tagEntities
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tagDictionary = tagEntities
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(t => t.Key, t => t.Name, StringComparer.OrdinalIgnoreCase);

            return (tagNames, tagDictionary);
        }

        private void SetMetaDescription(DirectoryEntry entry)
        {
            this.ViewData[StringConstants.MetaDescription] = entry.Description;
        }

        private async Task SetCanonicalAsync(string directoryEntryKey, int page)
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);

            string path = page <= 1
                ? UrlBuilder.ListingPath(directoryEntryKey)
                : $"{UrlBuilder.ListingPath(directoryEntryKey)}/page/{page}";

            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, path);
        }

        private async Task SetCategoryContextViewBagAsync(int subCategoryId)
        {
            var subcategory = await this.subCategoryRepository.GetByIdAsync(subCategoryId);
            var category = await this.categoryRepository.GetByIdAsync(subcategory.CategoryId);

            this.ViewBag.CategoryName = category.Name;
            this.ViewBag.SubCategoryName = subcategory.Name;
            this.ViewBag.CategoryKey = category.CategoryKey;
            this.ViewBag.SubCategoryKey = subcategory.SubCategoryKey;
        }

        private DirectoryEntryViewModel BuildDirectoryEntryViewModel(
            DirectoryEntry entry,
            string link2Name,
            string link3Name,
            List<string> tagNames,
            Dictionary<string, string> tagDictionary,
            bool isSponsor,
            List<string> additionalLinks)
        {
            return new DirectoryEntryViewModel
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                Name = entry.Name,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                Link = entry.Link,
                LinkA = entry.LinkA,
                Link2 = entry.Link2,
                Link2A = entry.Link2A,
                Link3 = entry.Link3,
                Link3A = entry.Link3A,
                DirectoryStatus = entry.DirectoryStatus,
                DirectoryBadge = entry.DirectoryBadge,
                Description = entry.Description,
                Location = entry.Location,
                Processor = entry.Processor,
                Note = entry.Note,
                Contact = entry.Contact,
                SubCategory = entry.SubCategory,
                SubCategoryId = entry.SubCategoryId,
                UpdateDate = entry.UpdateDate,
                CreateDate = entry.CreateDate,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Tags = tagNames,
                TagsAndKeys = tagDictionary,
                AdditionalLinks = additionalLinks ?? new List<string>(),
                CountryCode = entry.CountryCode,
                IsSponsored = isSponsor,
                PgpKey = entry.PgpKey,
                ProofLink = entry.ProofLink,
                VideoLink = entry.VideoLink,
                FoundedDate = entry.FoundedDate,
                FormattedLocation = BuildLocationHtml(entry.Location, entry.CountryCode, this.urlResolver)
            };
        }

        private async Task<(EntryReviewsBlockViewModel Vm, int EffectivePage)> BuildReviewsVmAsync(
            DirectoryEntry entry,
            int requestedPage,
            CancellationToken ct)
        {
            int totalReviews = await this.reviewRepository.CountApprovedForEntryAsync(entry.DirectoryEntryId, ct);

            var (c1, c2, c3, c4, c5) = totalReviews > 0
                ? await this.reviewRepository.GetApprovedRatingCountsForEntryAsync(entry.DirectoryEntryId, ct)
                : (0, 0, 0, 0, 0);

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalReviews / (double)IntegerConstants.ReviewsPageSize));
            int page = Math.Clamp(requestedPage, 1, totalPages);

            var reviews = await this.reviewRepository.ListApprovedForEntryAsync(
                entry.DirectoryEntryId,
                page: page,
                pageSize: IntegerConstants.ReviewsPageSize,
                ct) ?? new List<DirectoryEntryReview>();

            this.ApplyOwnerDisplayNames(entry, reviews);

            var reviewIds = reviews.Select(r => r.DirectoryEntryReviewId).ToList();

            var allReplies = reviewIds.Count == 0
                ? new List<DirectoryEntryReviewComment>()
                : await this.reviewCommentRepository.Query()
                    .Where(c =>
                        reviewIds.Contains(c.DirectoryEntryReviewId) &&
                        c.ModerationStatus == ReviewModerationStatus.Approved)
                    .OrderBy(c => c.CreateDate)
                    .ThenBy(c => c.DirectoryEntryReviewCommentId)
                    .ToListAsync(ct);

            this.ApplyOwnerDisplayNamesToReplies(entry, allReplies);

            var repliesLookup = allReplies
                .GroupBy(x => x.DirectoryEntryReviewId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var avg = totalReviews > 0
                ? await this.reviewRepository.AverageRatingForEntryApprovedAsync(entry.DirectoryEntryId, ct)
                : null;

            var authorCounts = await this.GetAuthorPostCountsCachedAsync(ct);

            var vm = new EntryReviewsBlockViewModel
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                Reviews = reviews,
                RepliesByReviewId = repliesLookup,
                ReviewCount = totalReviews,
                AverageRating = avg,
                Rating1Count = c1,
                Rating2Count = c2,
                Rating3Count = c3,
                Rating4Count = c4,
                Rating5Count = c5,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = IntegerConstants.ReviewsPageSize,
                AuthorPostCountsByFingerprint = authorCounts
            };

            return (vm, page);
        }

        private void ApplyOwnerDisplayNames(DirectoryEntry entry, List<DirectoryEntryReview> reviews)
        {
            if (reviews == null || reviews.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.PgpKey))
            {
                foreach (var r in reviews)
                {
                    r.DisplayName = string.IsNullOrWhiteSpace(r.DisplayName) ? null : r.DisplayName;
                }

                return;
            }

            var entryFps = PgpFingerprintTools.GetAllFingerprints(entry.PgpKey);

            foreach (var r in reviews)
            {
                var reviewNorm = PgpFingerprintTools.Normalize(r.AuthorFingerprint);
                bool isOwner = entryFps.Any(fp => PgpFingerprintTools.Matches(reviewNorm, fp));
                r.DisplayName = isOwner ? entry.Name : null;
            }
        }

        private void ApplyOwnerDisplayNamesToReplies(DirectoryEntry entry, List<DirectoryEntryReviewComment> replies)
        {
            if (replies == null || replies.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.PgpKey))
            {
                foreach (var c in replies)
                {
                    c.DisplayName = string.IsNullOrWhiteSpace(c.DisplayName) ? null : c.DisplayName;
                }

                return;
            }

            var entryFps = PgpFingerprintTools.GetAllFingerprints(entry.PgpKey);

            foreach (var c in replies)
            {
                var replyNorm = PgpFingerprintTools.Normalize(c.AuthorFingerprint);
                bool isOwner = entryFps.Any(fp => PgpFingerprintTools.Matches(replyNorm, fp));
                c.DisplayName = isOwner ? entry.Name : null;
            }
        }

        // -------------------------------------------------------------------------
        // Nested Types
        // -------------------------------------------------------------------------

        private sealed class TimelineItem
        {
            public int DirectoryEntryId { get; set; }
            public DateTime Effective { get; set; }
            public DirectoryManager.Data.Enums.DirectoryStatus DirectoryStatus { get; set; }
            public int? SubCategoryId { get; set; }
            public DirectoryManager.Data.Models.Subcategory? SubCategory { get; set; }
        }
    }
}
