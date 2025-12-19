using System.Net;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
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
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class DirectoryEntryController : BaseController
    {
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
            IUrlResolutionService urlResolver)
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
        }

        [Route("directoryentry/index")]
        public async Task<IActionResult> Index(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();
            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory != null && e.SubCategory.SubCategoryId == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllDtoAsync())
                                    .OrderBy(sc => sc.CategoryName)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return this.View(entries);
        }

        [Route("directoryentry/create")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await this.LoadLists();

            return this.View();
        }

        [Route("directoryentry/create")]
        [HttpPost]
        public async Task<IActionResult> Create(DirectoryEntry model)
        {
            if (!this.ModelState.IsValid ||
                model.DirectoryStatus == DirectoryStatus.Unknown ||
                model.SubCategoryId == 0)
            {
                await this.LoadLists();

                return this.View("create", model);
            }

            // Check if the link is already used
            var link = model.Link.Trim();
            var entryName = model.Name.Trim();
            var existingEntryByLink = await this.directoryEntryRepository.GetByLinkAsync(link);

            if (existingEntryByLink != null)
            {
                await this.LoadLists();

                this.ModelState.AddModelError("Link", "The provided link is already used by another entry.");
                return this.View("create", model);
            }

            var existingEntryByNameSubcat = await this.directoryEntryRepository.GetByNameAndSubcategoryAsync(entryName, model.SubCategoryId);

            if (existingEntryByNameSubcat != null)
            {
                await this.LoadLists();

                this.ModelState.AddModelError("Name", "The provided name is already used by another entry in this subcategory.");
                return this.View("create", model);
            }

            model.CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty;
            model.Link = link;
            model.Name = entryName;
            model.DirectoryEntryKey = StringHelpers.UrlKey(model.Name);
            model.Description = model.Description?.Trim();
            model.Note = model.Note?.Trim();
            model.Contact = model.Contact?.Trim();
            model.Location = model.Location?.Trim();
            model.Processor = model.Processor?.Trim();
            model.LinkA = model.LinkA?.Trim();
            model.Link2 = model.Link2?.Trim();
            model.Link2A = model.Link2A?.Trim();
            model.Link3 = model.Link3?.Trim();
            model.Link3A = model.Link3A?.Trim();
            model.PgpKey = model.PgpKey?.Trim();
            model.ProofLink = model.ProofLink?.Trim();
            model.VideoLink = model.VideoLink?.Trim();

            await this.directoryEntryRepository.CreateAsync(model);

            // now process tags
            if (!string.IsNullOrWhiteSpace(model.Tags))
            {
                var tagNames = model.Tags
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var name in tagNames)
                {
                    var normalizedName = FormattingHelper.NormalizeTagName(name);
                    var tag = await this.tagRepo.GetByKeyAsync(normalizedName.UrlKey())
                           ?? await this.tagRepo.CreateAsync(normalizedName);
                    await this.entryTagRepo.AssignTagAsync(model.DirectoryEntryId, tag.TagId);
                }
            }

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("directoryentry/edit/{id}")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (entry == null)
            {
                return this.NotFound();
            }

            await this.LoadSubCategories();

            var tags = await this.entryTagRepo.GetTagsForEntryAsync(id);
            entry.Tags = string.Join(", ", tags
                                         .OrderBy(t => t.Name)
                                         .Select(t => t.Name));

            await this.PopulateCountryDropDownList();

            return this.View(entry);  // Pass the entry model for editing
        }

        [Route("directoryentry/edit/{id}")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, DirectoryEntry entry)
        {
            var existingEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (existingEntry == null)
            {
                return this.NotFound();
            }

            existingEntry.UpdatedByUserId = this.userManager.GetUserId(this.User);
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.Link = entry.Link.Trim();
            existingEntry.LinkA = entry.LinkA?.Trim();
            existingEntry.Link2 = entry.Link2?.Trim();
            existingEntry.Link2A = entry.Link2A?.Trim();
            existingEntry.Link3 = entry.Link3?.Trim();
            existingEntry.Link3A = entry.Link3A?.Trim();
            existingEntry.ProofLink = entry.ProofLink?.Trim();
            existingEntry.VideoLink = entry.VideoLink?.Trim();
            existingEntry.Name = entry.Name.Trim();
            existingEntry.DirectoryEntryKey = StringHelpers.UrlKey(entry.Name);
            existingEntry.Description = entry.Description?.Trim();
            existingEntry.Note = entry.Note?.Trim();
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Contact = entry.Contact?.Trim();
            existingEntry.Location = entry.Location?.Trim();
            existingEntry.Processor = entry.Processor?.Trim();
            existingEntry.CountryCode = entry.CountryCode;
            existingEntry.PgpKey = entry.PgpKey?.Trim();

            await this.directoryEntryRepository.UpdateAsync(existingEntry);

            // Re-sync tags: remove all, then add back from comma list
            var newTagNames = (entry.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 1) remove any tags not in the new list
            var currentTags = await this.entryTagRepo.GetTagsForEntryAsync(id);
            foreach (var tag in currentTags)
            {
                if (!newTagNames.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
                {
                    await this.entryTagRepo.RemoveTagAsync(id, tag.TagId);
                }
            }

            // 2) add any new tags
            foreach (var name in newTagNames)
            {
                var normalizedName = FormattingHelper.NormalizeTagName(name);
                var tag = await this.tagRepo.GetByKeyAsync(normalizedName.UrlKey())
                       ?? await this.tagRepo.CreateAsync(normalizedName);
                await this.entryTagRepo.AssignTagAsync(id, tag.TagId);
            }

            this.ClearCachedItems();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        [Route("directoryentry/entryaudits/{entryId}")]
        public async Task<IActionResult> EntryAudits(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsWithSubCategoriesForEntryAsync(entryId);
            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(entryId);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            this.ViewBag.SelectedDirectoryEntry = new DirectoryEntryViewModel()
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

            // Set category and subcategory names for each audit entry
            foreach (var audit in audits)
            {
                if (audit.SubCategory != null)
                {
                    audit.SubCategoryName = $"{audit.SubCategory.Category?.Name} > {audit.SubCategory.Name}";
                }
                else
                {
                    audit.SubCategoryName = "No SubCategory Assigned";
                }
            }

            return this.View(audits);
        }

        [HttpGet("directoryentry/delete")]
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
            if (bytes.Length == 0)
            {
                return this.File(Array.Empty<byte>(), StringConstants.PngImage);
            }

            return this.File(bytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/monthlyplotimage")]
        public async Task<IActionResult> MonthlyPlotImageAsync()
        {
            DirectoryEntryPlotting plottingChart = new DirectoryEntryPlotting();
            var entries = await this.auditRepository.GetAllAsync();
            var imageBytes = plottingChart.CreateMonthlyActivePlot(entries.ToList());
            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/categorypiechart")]
        public async Task<IActionResult> CategoryPieChartImageAsync()
        {
            DirectoryEntryPlotting plottingChart = new DirectoryEntryPlotting();
            var allCategories = await this.categoryRepository.GetActiveCategoriesAsync();
            var entries = await this.directoryEntryRepository.GetAllActiveEntries();
            var imageBytes = plottingChart.CreateCategoryPieChartImage(entries, allCategories);
            return this.File(imageBytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/countrieschart")]
        public async Task<IActionResult> CountryPlotImageAsync()
        {
            var entries = await this.directoryEntryRepository.GetAllActiveEntries();

            // keep only entries with a non-empty, recognized ISO-2 country code
            var knownCountries = CountryHelper.GetCountries(); // key = ISO code (upper)
            var filtered = (entries ?? Enumerable.Empty<DirectoryEntry>())
                .Where(e => !string.IsNullOrWhiteSpace(e.CountryCode)
                         && knownCountries.ContainsKey(e.CountryCode!.Trim().ToUpperInvariant()))
                .ToList();

            var plottingChart = new DirectoryEntryPlotting();
            var imageBytes = plottingChart.CreateActiveCountriesPieChartImage(filtered);

            return this.File(imageBytes.Length == 0 ? Array.Empty<byte>() : imageBytes, StringConstants.PngImage);
        }

        [HttpGet("directoryentry/subcategory-trends")]
        public async Task<IActionResult> SubcategoryTrends([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            const string unknown = "(Unknown)";
            const int UnknownSubCatKey = -1; // sentinel for "null" subcategory keys

            DateTime to = end ?? DateTime.UtcNow;
            DateTime from = start ?? to.AddYears(-1);

            // widen fetch a bit so we can resolve "latest <= from"
            DateTime preloadFrom = from.AddYears(-5);
            var audits = await this.auditRepository.GetAllWithSubcategoriesAsync(preloadFrom, to);

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
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.Effective).ToList());

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

            // Use non-nullable int keys + sentinel
            var startCounts = new Dictionary<int, (string name, int count)>();
            var endCounts = new Dictionary<int, (string name, int count)>();

            foreach (var kvp in byEntry)
            {
                var timeline = kvp.Value;

                var (sid, sname, sactive) = StateAt(from, timeline);
                if (sactive)
                {
                    var sKey = sid ?? UnknownSubCatKey;
                    if (!startCounts.TryGetValue(sKey, out var s))
                    {
                        startCounts[sKey] = (sname, 1);
                    }
                    else
                    {
                        startCounts[sKey] = (s.name, s.count + 1);
                    }
                }

                var (eid, ename, eactive) = StateAt(to, timeline);
                if (eactive)
                {
                    var eKey = eid ?? UnknownSubCatKey;
                    if (!endCounts.TryGetValue(eKey, out var e))
                    {
                        endCounts[eKey] = (ename, 1);
                    }
                    else
                    {
                        endCounts[eKey] = (e.name, e.count + 1);
                    }
                }
            }

            // Merge keys safely (non-nullable)
            var allIds = new HashSet<int>(startCounts.Keys.Concat(endCounts.Keys));

            var trends = new List<SubcategoryTrendItem>();
            foreach (var key in allIds)
            {
                var s = startCounts.TryGetValue(key, out var sv) ? sv : (name: unknown, count: 0);
                var e = endCounts.TryGetValue(key, out var ev) ? ev : (name: s.name, count: 0);

                trends.Add(new SubcategoryTrendItem
                {
                    // convert sentinel back to nullable for the view model
                    SubCategoryId = key == UnknownSubCatKey ? (int?)null : key,
                    SubCategoryName = string.IsNullOrWhiteSpace(e.name) ? s.name : e.name,
                    StartCount = s.count,
                    EndCount = e.count
                });
            }

            var countToTake = 100;

            var topGrowth = trends.OrderByDescending(t => t.PercentChange)
                                  .ThenByDescending(t => t.Delta)
                                  .Take(countToTake).ToList();

            var topDecline = trends.OrderBy(t => t.PercentChange)
                                   .ThenBy(t => t.Delta)
                                   .Take(countToTake).ToList();

            var vm = new SubcategoryTrendsReportViewModel
            {
                Start = from,
                End = to,
                TopGrowth = topGrowth,
                TopDecline = topDecline
            };

            return this.View("SubcategoryTrends", vm);
        }

        [AllowAnonymous]
        [HttpGet("{categorykey}/{subcategorykey}/{directoryEntryKey}")]
        public async Task<IActionResult> DirectoryEntryView(string categoryKey, string subCategoryKey, string directoryEntryKey)
        {
            var canoicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canoicalDomain, $"{categoryKey}/{subCategoryKey}/{directoryEntryKey}");
            var category = await this.categoryRepository.GetByKeyAsync(categoryKey);

            if (category == null)
            {
                return this.NotFound();
            }

            var subCategory = await this.subCategoryRepository.GetByCategoryIdAndKeyAsync(category.CategoryId, subCategoryKey);

            if (subCategory == null)
            {
                return this.NotFound();
            }

            var existingEntry = await this.directoryEntryRepository.GetBySubCategoryAndKeyAsync(subCategory.SubCategoryId, directoryEntryKey);

            if (existingEntry == null || existingEntry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            this.ViewData[StringConstants.MetaDescription] = existingEntry.Description;

            var link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name);

            var tagEntities = await this.entryTagRepo.GetTagsForEntryAsync(existingEntry.DirectoryEntryId);
            var tagNames = tagEntities
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToList();

            var tagDictionary = tagEntities
                .OrderBy(t => t.Name)
                .ToDictionary(
                    t => t.Key,
                    t => t.Name);

            // Get/cached active sponsors (typed, null-safe)
            const string sponsorCacheKey = StringConstants.CacheKeyAllActiveSponsors;

            var allSponsors = await this.cache.GetOrCreateAsync(sponsorCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                var sponsors = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
                return sponsors?.ToList() ?? new List<SponsoredListing>();
            }) ?? new List<SponsoredListing>();

            var approved = await this.reviewRepository
                .Query()
                .Where(r => r.DirectoryEntryId == existingEntry.DirectoryEntryId
                         && r.ModerationStatus == ReviewModerationStatus.Approved)
                .OrderByDescending(r => r.UpdateDate ?? r.CreateDate)
                .ToListAsync();

            double? average = null;
            var rated = approved.Where(r => r.Rating.HasValue).ToList();
            if (rated.Count > 0)
            {
                average = rated.Average(r => (double)r.Rating!.Value);
            }

            // Try to get a normalized fingerprint from the entry's PGP key text
            string? entryFp = PgpFingerprintTools.GetFingerprintFromArmored(existingEntry.PgpKey);
            string entryFpNorm = NormalizeFp(entryFp);

            // collect ALL fingerprints from the entry's armored key (primary + subkeys)
            var entryFps = PgpFingerprintTools.GetAllFingerprints(existingEntry.PgpKey); // HashSet<string> (UPPER hex, includes short forms)

            // ... build reviews VM
            var reviewsVm = new EntryReviewsBlockViewModel
            {
                DirectoryEntryId = existingEntry.DirectoryEntryId,
                DirectoryEntryName = existingEntry.Name,
                AverageRating = average,
                ReviewCount = approved.Count,
                Reviews = approved.Select(r =>
                {
                    string reviewNorm = PgpFingerprintTools.Normalize(r.AuthorFingerprint);
                    bool isOwner = entryFps.Any(fp => PgpFingerprintTools.Matches(reviewNorm, fp));

                    return new EntryReviewItem
                    {
                        Rating = r.Rating,
                        Body = r.Body,
                        AuthorFingerprint = r.AuthorFingerprint,
                        AuthorDisplay = isOwner ? existingEntry.Name : r.AuthorFingerprint,
                        CreateDate = r.CreateDate
                    };
                }).ToList()
            };

            this.ViewBag.ReviewsVm = reviewsVm;

            // flag if this entry is a sponsor
            bool isSponsor = allSponsors == null ? false : allSponsors.Any(s => s.DirectoryEntryId == existingEntry.DirectoryEntryId);

            var model = new DirectoryEntryViewModel
            {
                DirectoryEntryId = existingEntry.DirectoryEntryId,
                Name = existingEntry.Name,
                DirectoryEntryKey = existingEntry.DirectoryEntryKey,
                Link = existingEntry.Link,
                LinkA = existingEntry.LinkA,
                Link2 = existingEntry.Link2,
                Link2A = existingEntry.Link2A,
                Link3 = existingEntry.Link3,
                Link3A = existingEntry.Link3A,
                DirectoryStatus = existingEntry.DirectoryStatus,
                DirectoryBadge = existingEntry.DirectoryBadge,
                Description = existingEntry.Description,
                Location = existingEntry.Location,
                Processor = existingEntry.Processor,
                Note = existingEntry.Note,
                Contact = existingEntry.Contact,
                SubCategory = existingEntry.SubCategory,
                SubCategoryId = existingEntry.SubCategoryId,
                UpdateDate = existingEntry.UpdateDate,
                CreateDate = existingEntry.CreateDate,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Tags = tagNames,
                TagsAndKeys = tagDictionary,
                CountryCode = existingEntry.CountryCode,
                IsSponsored = isSponsor,
                PgpKey = existingEntry.PgpKey,
                ProofLink = existingEntry.ProofLink,
                VideoLink = existingEntry.VideoLink,
                FormattedLocation = BuildLocationHtml(existingEntry.Location, existingEntry.CountryCode, this.urlResolver)
            };

            this.ViewBag.CategoryName = category.Name;
            this.ViewBag.SubCategoryName = subCategory.Name;
            this.ViewBag.CategoryKey = categoryKey;
            this.ViewBag.SubCategoryKey = subCategoryKey;

            return this.View("DirectoryEntryView", model);
        }

        // Normalize any fingerprint (strip spaces/separators, upper-case)
        private static string NormalizeFp(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            string hex = Regex.Replace(s, @"[^0-9A-Fa-f]", ""); // keep hex only
            return hex.ToUpperInvariant();
        }

        private static string BuildLocationHtml(string? locationRaw, string? countryCode, IUrlResolutionService urlResolver)
        {
            var location = locationRaw?.Trim();
            var ccRaw = countryCode?.Trim();

            // Prepare flag (show whenever we have a country code)
            string flagHtml = string.Empty;
            if (!string.IsNullOrWhiteSpace(ccRaw))
            {
                var ccLower = ccRaw.ToLowerInvariant();
                var countryNameForAlt = CountryHelper.GetCountryName(ccRaw) ?? string.Empty;
                var altTitle = string.IsNullOrWhiteSpace(countryNameForAlt)
                    ? $"Flag ({ccRaw})"
                    : $"Flag of {countryNameForAlt} ({ccRaw.ToUpperInvariant()})";

                // margin-right so text doesn’t collide with the flag
                flagHtml = $"<img class=\"country-flag me-2 align-text-bottom\" src=\"/images/flags/{ccLower}.png\" alt=\"{WebUtility.HtmlEncode(altTitle)}\" title=\"{WebUtility.HtmlEncode(countryNameForAlt)}\" /> ";
            }

            // Compute country name + link (if available)
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

            // Nothing else to show
            if (string.IsNullOrWhiteSpace(location) && string.IsNullOrWhiteSpace(countryName))
            {
                return flagHtml; // could be empty if no cc
            }

            // If we don't have a country name/link, just emit flag + encoded location
            if (string.IsNullOrWhiteSpace(countryName) || string.IsNullOrWhiteSpace(anchorHtml))
            {
                return $"{flagHtml}{WebUtility.HtmlEncode(location ?? string.Empty)}";
            }

            // Do we already have the country name inside the location string?
            var idx = !string.IsNullOrWhiteSpace(location)
                ? location!.IndexOf(countryName!, StringComparison.OrdinalIgnoreCase)
                : -1;

            if (idx >= 0)
            {
                // Replace first occurrence with a link, preserve punctuation (no extra spaces before commas)
                var before = location!.Substring(0, idx);
                var after = location!.Substring(idx + countryName!.Length);
                return $"{flagHtml}{WebUtility.HtmlEncode(before)}{anchorHtml}{WebUtility.HtmlEncode(after)}";
            }
            else if (!string.IsNullOrWhiteSpace(location))
            {
                // Append ", <linked country>" with exactly one comma+space
                var left = location!.TrimEnd();
                if (left.EndsWith(",", StringComparison.Ordinal))
                {
                    left += " ";
                }
                else
                {
                    left = left + ", ";
                }

                return $"{flagHtml}{WebUtility.HtmlEncode(left)}{anchorHtml}";
            }
            else
            {
                // No location text; only the linked country (with flag)
                return $"{flagHtml}{anchorHtml}";
            }
        }

        private async Task LoadLists()
        {
            await this.LoadSubCategories();
            await this.PopulateCountryDropDownList();
        }

        private async Task PopulateCountryDropDownList(object? selectedId = null)
        {
            // Get the dictionary of countries from the helper.
            var countries = CountryHelper.GetCountries();

            countries = countries.OrderBy(x => x.Value).ToDictionary<string, string>();

            // Build a list of SelectListItem from the dictionary.
            var list = countries.Select(c => new SelectListItem
            {
                Value = c.Key,
                Text = c.Value
            }).ToList();

            // Insert default option at the top.
            list.Insert(0, new SelectListItem { Value = "", Text = StringConstants.SelectText });
            this.ViewBag.CountryCode = new SelectList(list, "Value", "Text", selectedId);
            await Task.CompletedTask; // For async signature compliance.
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