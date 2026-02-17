using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Migrations;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Utilities;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class SubmissionController : BaseController
    {
        private const int MaxLinks = DirectoryManager.Web.Constants.IntegerConstants.MaxAdditionalLinks;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ISubmissionRepository submissionRepository;
        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IBlockedIPRepository blockedIPRepository;
        private readonly IMemoryCache cache;
        private readonly ICacheService cacheHelper;
        private readonly ITagRepository tagRepo;
        private readonly IDirectoryEntryTagRepository entryTagRepo;
        private readonly IAdditionalLinkRepository additionalLinkRepo;
        private readonly IDomainRegistrationDateService domainRegistrationDateService;

        public SubmissionController(
            UserManager<ApplicationUser> userManager,
            ISubmissionRepository submissionRepository,
            ISubcategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            IDirectoryEntriesAuditRepository auditRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IBlockedIPRepository blockedIPRepository,
            IMemoryCache cache,
            ICacheService cacheHelper,
            ITagRepository tagRepo,
            IDirectoryEntryTagRepository entryTagRepo,
            IAdditionalLinkRepository additionalLinkRepo,
            IDomainRegistrationDateService domainRegistrationDateService)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.submissionRepository = submissionRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.auditRepository = auditRepository;
            this.blockedIPRepository = blockedIPRepository;
            this.cache = cache;
            this.cacheHelper = cacheHelper;
            this.tagRepo = tagRepo;
            this.entryTagRepo = entryTagRepo;
            this.additionalLinkRepo = additionalLinkRepo;
            this.domainRegistrationDateService = domainRegistrationDateService;
        }

        [AllowAnonymous]
        [HttpGet("submit")]
        public async Task<IActionResult> CreateAsync(int? submissionId)
        {
            var model = new SubmissionRequest();

            if (submissionId != null)
            {
                var submission = await this.submissionRepository.GetByIdAsync(submissionId.Value);

                if (submission == null || submission.SubmissionStatus != SubmissionStatus.Preview)
                {
                    return this.BadRequest(Constants.StringConstants.SubmissionAlreadySubmitted);
                }

                model = GetSubmissionCreateModel(submission);

                // ✅ pre-check based on persisted CSV
                model.SelectedTagIdsCsv = submission.SelectedTagIdsCsv;
                model.SelectedTagIds = ParseCsvIds(submission.SelectedTagIdsCsv);
            }

            await this.LoadDropDowns();
            await this.LoadAllTagsForCheckboxesAsync();
            return this.View(model);
        }

        [AllowAnonymous]
        [HttpPost("submit")]
        public async Task<IActionResult> Create(SubmissionRequest model)
        {
            // ---- Validate URLs ----

            if (!UrlHelper.IsValidUrl(model.Link ?? string.Empty))
            {
                this.ModelState.AddModelError(nameof(model.Link), "The link is not a valid URL.");
            }

            // Link2/Link3 are Tor/I2P/alt links (still validate as URLs if supplied)
            if (!string.IsNullOrWhiteSpace(model.Link2) && !UrlHelper.IsValidUrl(model.Link2))
            {
                this.ModelState.AddModelError(nameof(model.Link2), "The link 2 is not a valid URL.");
            }

            if (!string.IsNullOrWhiteSpace(model.Link3) && !UrlHelper.IsValidUrl(model.Link3))
            {
                this.ModelState.AddModelError(nameof(model.Link3), "The link 3 is not a valid URL.");
            }

            if (!string.IsNullOrWhiteSpace(model.ProofLink) && !UrlHelper.IsValidUrl(model.ProofLink))
            {
                this.ModelState.AddModelError(nameof(model.ProofLink), "The proof link is not a valid URL.");
            }

            if (!string.IsNullOrWhiteSpace(model.VideoLink) && !UrlHelper.IsValidUrl(model.VideoLink))
            {
                this.ModelState.AddModelError(nameof(model.VideoLink), "The video link is not a valid URL.");
            }

            // Related/Additional links (forum post / docs / proof page, etc.)
            var relatedLinks = NormalizeLinks(
                new[] { model.RelatedLink1, model.RelatedLink2, model.RelatedLink3 },
                MaxLinks);

            for (int i = 0; i < relatedLinks.Count; i++)
            {
                if (!UrlHelper.IsValidUrl(relatedLinks[i]))
                {
                    this.ModelState.AddModelError(string.Empty, $"Related link {i + 1} is not a valid URL.");
                }
            }

            // PGP Key validation
            if (!string.IsNullOrWhiteSpace(model.PgpKey) && !PgpKeyValidator.IsValid(model.PgpKey))
            {
                this.ModelState.AddModelError(
                    nameof(model.PgpKey),
                    "The PGP public key block you entered is not valid. " +
                    "Please supply a valid ASCII-armored PGP public key.");
            }

            // ---- Spam / block checks ----

            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            if (ScriptValidation.ContainsScriptTag(model) || this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.RedirectToAction("Success", "Submission");
            }

            // ---- Persist checkbox selections into CSV (no JS required) ----

            model.SelectedTagIds = model.SelectedTagIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            model.SelectedTagIdsCsv = model.SelectedTagIds.Count == 0
                ? null
                : string.Join(",", model.SelectedTagIds);

            if (!TryParseFoundedDateParts(model.FoundedYear, model.FoundedMonth, model.FoundedDay, out var foundedDate, out var foundedErr))
            {
                this.ModelState.AddModelError(nameof(model.FoundedYear), foundedErr!);
            }

            // ---- If invalid, reload dropdowns + tag list and return to SubmitEdit ----

            if (!this.ModelState.IsValid)
            {
                await this.LoadDropDowns();
                await this.LoadAllTagsForCheckboxesAsync();
                return this.View("SubmitEdit", model);
            }

            // ---- Create/update preview submission ----
            return await this.CreateSubmission(model);
        }

        [AllowAnonymous]
        [HttpGet("submission/preview/{id}")]
        public async Task<IActionResult> Preview(int id)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);

            if (submission == null)
            {
                return this.BadRequest(Constants.StringConstants.SubmissionDoesNotExist);
            }

            var model = await this.GetSubmissionPreviewAsync(submission);

            return this.View(model);
        }

        [AllowAnonymous]
        [HttpGet("submission/submitedit/{id}")]
        public async Task<IActionResult> SubmitEdit(int id, CancellationToken ct)
        {
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            var entryTags = await this.entryTagRepo.GetTagsForEntryAsync(id);

            var model = GetSubmissionRequestModel(directoryEntry);
            model.Tags = string.Join(
                ", ",
                entryTags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Select(t => t.Name));

            // ✅ pre-check existing tag ids
            model.SelectedTagIds = entryTags.Select(t => t.TagId).Distinct().ToList();
            model.SelectedTagIdsCsv = string.Join(",", model.SelectedTagIds);

            // ✅ LOAD existing "additional links" for this listing and map to the 3 inputs
            var additional = await this.additionalLinkRepo.GetByDirectoryEntryIdAsync(id, ct);
            var urls = (additional ?? new List<AdditionalLink>())
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.AdditionalLinkId)
                .Select(x => x.Link)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxLinks)
                .ToList();

            model.RelatedLink1 = urls.ElementAtOrDefault(0);
            model.RelatedLink2 = urls.ElementAtOrDefault(1);
            model.RelatedLink3 = urls.ElementAtOrDefault(2);

            await this.SetSelectSubCategoryViewBag();
            await this.LoadDropDowns();
            await this.LoadAllTagsForCheckboxesAsync();

            return this.View("SubmitEdit", model);
        }


        [AllowAnonymous]
        [HttpGet("submission/findexisting")]
        public async Task<IActionResult> FindExisting(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory?.SubCategoryId == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllDtoAsync())
                                    .OrderBy(sc => sc.CategoryName)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return this.View(entries);
        }

        [HttpGet("submission/audit/{entryId}")]
        public async Task<IActionResult> AuditAync(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsWithSubCategoriesForEntryAsync(entryId);
            var link2Name = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link3Name);
            var canonicalDomain = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(entryId);

            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            var directoryItem = ViewModelConverter.ConvertToViewModels([directoryEntry]).First();
            directoryItem.ItemPath = UrlBuilder.CombineUrl(canonicalDomain, directoryItem.ItemPath);

            this.ViewBag.SelectedDirectoryEntry = directoryItem;

            // Set category and subcategory names for each audit entry
            foreach (var audit in audits)
            {
                if (audit.SubCategory != null)
                {
                    audit.SubCategoryName = $"{audit.SubCategory.Category?.Name} > {audit.SubCategory.Name}";
                }
                else
                {
                    audit.SubCategoryName = "No Subcategory Assigned";
                }
            }

            return this.View("Audit", audits);
        }

        [Authorize]
        [HttpGet("submission/index")]
        public async Task<IActionResult> Index(int? page, int pageSize = Constants.IntegerConstants.DefaultPageSize)
        {
            int pageNumber = page ?? 1;
            var submissions = await this.submissionRepository.GetAllAsync();
            var count = submissions.Count();
            var items = submissions.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var viewModel = new SubmissionPagedList
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = count,
                Items = items
            };

            return this.View(viewModel);
        }

        [Authorize]
        [HttpGet("submission/{id}")]
        public async Task<IActionResult> Review(int id, CancellationToken ct)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            // ----------------------------
            // Load all tags for checkbox list (ViewBag.AllTags)
            // ----------------------------
            var allTags = await this.tagRepo.ListAllAsync();
            this.ViewBag.AllTags = allTags
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new TagOptionVm { TagId = t.TagId, Name = t.Name })
                .ToList();

            // ----------------------------
            // Determine selected tag IDs (from submission CSV, with fallback to current listing tags)
            // ----------------------------
            var selectedIds = (submission.SelectedTagIdsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x => int.TryParse(x, out var id2) ? id2 : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();

            if (selectedIds.Count == 0 && submission.DirectoryEntryId.HasValue)
            {
                var current = await this.entryTagRepo.GetTagsForEntryAsync(submission.DirectoryEntryId.Value);
                selectedIds = current.Select(t => t.TagId).ToHashSet();
            }

            this.ViewBag.SelectedTagIds = selectedIds;

            // Selected tag names (for diff output)
            var selectedTagNames = allTags
                .Where(t => selectedIds.Contains(t.TagId))
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // ----------------------------
            // Compare submission vs existing entry (includes tags + related links)
            // ----------------------------
            if (submission.DirectoryEntryId.HasValue)
            {
                if (submission.SubCategory == null && submission.SubCategoryId.HasValue)
                {
                    submission.SubCategory = await this.subCategoryRepository
                        .GetByIdAsync(submission.SubCategoryId.Value);
                }

                var existing = await this.directoryEntryRepository
                    .GetByIdAsync(submission.DirectoryEntryId.Value);

                if (existing != null)
                {
                    // existing/current entry tag names
                    var existingTags = await this.entryTagRepo.GetTagsForEntryAsync(existing.DirectoryEntryId);
                    var existingTagNames = existingTags
                        .Select(t => t.Name)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // optional: keep existing tag string for other UI
                    existing.Tags = string.Join(", ", existingTagNames);

                    // existing/current entry additional links (for related link diff)
                    var existingAdditional = await this.additionalLinkRepo
                        .GetByDirectoryEntryIdAsync(existing.DirectoryEntryId, ct);

                    var entryRelatedLinks = (existingAdditional ?? new List<AdditionalLink>())
                        .OrderBy(x => x.SortOrder)
                        .ThenBy(x => x.AdditionalLinkId)
                        .Select(x => x.Link)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(MaxLinks)
                        .ToList();

                    this.ViewBag.Differences = ModelComparisonHelpers.CompareEntries(
                        existing,
                        submission,
                        entryTagNames: existingTagNames,
                        selectedTagNames: selectedTagNames,
                        entryRelatedLinks: entryRelatedLinks);
                }
            }

            await this.LoadDropDowns();
            await this.SetSelectSubCategoryViewBag();

            return this.View(submission);
        }

        [Authorize]
        [HttpPost("submission/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(
            int id,
            Submission model,
            int[] selectedTagIds,
            List<string>? relatedLinks,
            string? foundedYear,
            string? foundedMonth,
            string? foundedDay)
        {
            relatedLinks ??= new List<string>();

            // normalize
            var normalizedRelated = relatedLinks
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            // validate
            for (int i = 0; i < normalizedRelated.Count; i++)
            {
                if (!UrlHelper.IsValidUrl(normalizedRelated[i]))
                {
                   this.ModelState.AddModelError(string.Empty, $"Related link {i + 1} is not a valid URL.");
                }
            }

            if (!TryParseFoundedDateParts(foundedYear, foundedMonth, foundedDay, out var foundedDate, out var foundedErr))
            {
                this.ModelState.AddModelError("FoundedYear", foundedErr!);
            }
            else
            {
                model.FoundedDate = foundedDate; // keep it on the posted model for redisplay
            }

            if (!this.ModelState.IsValid)
            {
                await this.LoadDropDowns();
                await this.SetSelectSubCategoryViewBag();
                await this.LoadAllTagsForCheckboxesAsync();

                this.ViewBag.SelectedTagIds = (selectedTagIds ?? Array.Empty<int>())
                    .Where(x => x > 0)
                    .ToHashSet();

                // re-populate the model so the form keeps values
                model.RelatedLinks = normalizedRelated;

                return this.View(model);
            }

            if (model.DirectoryStatus == DirectoryStatus.Unknown)
            {
                throw new Exception($"Invalid directory status: {model.DirectoryStatus}");
            }

            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            // typed tags are suggestions only
            submission.Tags = model.Tags?.Trim();

            // checkbox tags = real tags
            var selected = (selectedTagIds ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToArray();

            submission.SelectedTagIdsCsv = selected.Length == 0 ? null : string.Join(",", selected);
            submission.FoundedDate = foundedDate;

            if (submission.SubmissionStatus == SubmissionStatus.Pending
                && model.SubmissionStatus == SubmissionStatus.Approved)
            {
                if (model.SubCategoryId == null || model.SubCategoryId == 0)
                {
                    return this.BadRequest(new { Error = "Submission does not have a subcategory" });
                }

                int entryId;

                if (model.DirectoryEntryId == null)
                {
                    await this.CreateDirectoryEntry(model);
                    var created = await this.directoryEntryRepository.GetByLinkAsync((model.Link ?? string.Empty).Trim());
                    entryId = created?.DirectoryEntryId
                        ?? throw new Exception("Failed to locate newly created entry");
                }
                else
                {
                    await this.UpdateDirectoryEntry(model);
                    entryId = model.DirectoryEntryId.Value;
                }

                // SYNC TAGS ON LISTING (checkboxes only)
                var existingTags = await this.entryTagRepo.GetTagsForEntryAsync(entryId);
                var existingIds = existingTags.Select(t => t.TagId).ToHashSet();
                var selectedIds = selected.ToHashSet();

                foreach (var oldId in existingIds.Except(selectedIds))
                {
                    await this.entryTagRepo.RemoveTagAsync(entryId, oldId);
                }

                foreach (var newId in selectedIds.Except(existingIds))
                {
                    await this.entryTagRepo.AssignTagAsync(entryId, newId);
                }

                this.ClearCachedItems();
            }

            submission.SubmissionStatus = model.SubmissionStatus;
            submission.CountryCode = model.CountryCode;

            await this.submissionRepository.UpdateAsync(submission);

            return this.RedirectToAction(nameof(this.Index));
        }

        [Authorize]
        [HttpGet("submission/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.submissionRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        [AllowAnonymous]
        [HttpGet("submission/success")]
        public IActionResult Success()
        {
            return this.View("Success");
        }

        [AllowAnonymous]
        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmAsync(int submissionId, CancellationToken ct)
        {
            var submission = await this.submissionRepository.GetByIdAsync(submissionId);

            if (submission == null)
            {
                return this.BadRequest(Constants.StringConstants.SubmissionDoesNotExist);
            }

            if (submission.SubmissionStatus == SubmissionStatus.Pending)
            {
                return this.BadRequest(Constants.StringConstants.SubmissionAlreadySubmitted);
            }

            // ✅ If user didn't provide FoundedDate during preview creation,
            // set it now (final submission), using:
            // 1) existing directory entry founded date if present, else
            // 2) domain registration date lookup from the link
            if (submission.FoundedDate is null)
            {
                // 1) Prefer existing listing's founded date if this is tied to an existing entry
                if (submission.DirectoryEntryId.HasValue && submission.DirectoryEntryId.Value > 0)
                {
                    var existing = await this.directoryEntryRepository.GetByIdAsync(submission.DirectoryEntryId.Value);
                    if (existing?.FoundedDate is not null)
                    {
                        submission.FoundedDate = existing.FoundedDate;
                    }
                }

                // 2) Otherwise try domain registration lookup
                if (submission.FoundedDate is null && !string.IsNullOrWhiteSpace(submission.Link))
                {
                    var lookedUp = await this.domainRegistrationDateService
                        .GetDomainRegistrationDateAsync(submission.Link, ct)
                        .ConfigureAwait(false);

                    if (lookedUp.HasValue)
                    {
                        submission.FoundedDate = lookedUp.Value;
                    }
                }
            }

            submission.SubmissionStatus = SubmissionStatus.Pending;

            await this.submissionRepository.UpdateAsync(submission);

            return this.View("Success");
        }

        private static bool TryParseFoundedDateParts(
            string? yearRaw,
            string? monthRaw,
            string? dayRaw,
            out DateOnly? founded,
            out string? error)
        {
            founded = null;
            error = null;

            var y = (yearRaw ?? "").Trim();
            var m = (monthRaw ?? "").Trim();
            var d = (dayRaw ?? "").Trim();

            // all blank => treat as NULL
            if (string.IsNullOrWhiteSpace(y) &&
                string.IsNullOrWhiteSpace(m) &&
                string.IsNullOrWhiteSpace(d))
            {
                return true;
            }

            // any provided => require all 3
            if (string.IsNullOrWhiteSpace(y) ||
                string.IsNullOrWhiteSpace(m) ||
                string.IsNullOrWhiteSpace(d))
            {
                error = "Founded date requires Year, Month, and Day (YYYY MM DD).";
                return false;
            }

            if (!int.TryParse(y, out var yy) ||
                !int.TryParse(m, out var mm) ||
                !int.TryParse(d, out var dd))
            {
                error = "Founded date must be numeric (YYYY MM DD).";
                return false;
            }

            // sanity (optional)
            if (yy < 1000 || yy > DateTime.UtcNow.Year + 1)
            {
                error = "Founded year looks invalid.";
                return false;
            }

            try
            {
                founded = new DateOnly(yy, mm, dd);
                return true;
            }
            catch
            {
                error = "Founded date is not a real calendar date.";
                return false;
            }
        }

        private static SubmissionRequest GetSubmissionRequestModel(Data.Models.DirectoryEntry directoryEntry)
        {
            return new SubmissionRequest()
            {
                Contact = directoryEntry.Contact,
                Description = directoryEntry.Description ?? string.Empty,
                Link = directoryEntry.Link,
                Link2 = directoryEntry.Link2 ?? string.Empty,
                Link3 = directoryEntry.Link3 ?? string.Empty,
                Location = directoryEntry.Location,
                ProofLink = directoryEntry.ProofLink,
                VideoLink = directoryEntry.VideoLink,
                Name = directoryEntry.Name,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
                DirectoryEntryId = directoryEntry.DirectoryEntryId,
                DirectoryStatus = directoryEntry.DirectoryStatus,
                CountryCode = directoryEntry.CountryCode,
                PgpKey = directoryEntry.PgpKey,
                RelatedLink1 = null,
                RelatedLink2 = null,
                RelatedLink3 = null,
                FoundedYear = directoryEntry.FoundedDate?.Year.ToString("0000"),
                FoundedMonth = directoryEntry.FoundedDate?.Month.ToString("00"),
                FoundedDay = directoryEntry.FoundedDate?.Day.ToString("00"),

            };
        }

        private static SubmissionRequest GetSubmissionCreateModel(Submission submission)
        {
            var related = submission.RelatedLinks ?? new List<string>();

            return new SubmissionRequest()
            {
                SubCategoryId = submission.SubCategoryId == null ? null : submission.SubCategoryId,
                NoteToAdmin = submission.NoteToAdmin,
                Contact = submission.Contact,
                Description = submission.Description,
                DirectoryEntryId = submission.DirectoryEntryId,
                DirectoryStatus = submission.DirectoryStatus,
                Link = submission.Link,
                Link2 = submission.Link2,
                Link3 = submission.Link3,
                ProofLink = submission.ProofLink,
                VideoLink = submission.VideoLink,
                Location = submission.Location,
                Name = submission.Name,
                Note = submission.Note,
                Processor = submission.Processor,
                SuggestedSubCategory = submission.SuggestedSubCategory,
                Tags = submission.Tags,
                CountryCode = submission.CountryCode,
                PgpKey = submission.PgpKey,
                RelatedLink1 = related.ElementAtOrDefault(0),
                RelatedLink2 = related.ElementAtOrDefault(1),
                RelatedLink3 = related.ElementAtOrDefault(2),
                FoundedYear = submission.FoundedDate?.Year.ToString("0000"),
                FoundedMonth = submission.FoundedDate?.Month.ToString("00"),
                FoundedDay = submission.FoundedDate?.Day.ToString("00"),

            };
        }

        private async Task<int?> GetExistingEntryAsync(SubmissionRequest request)
        {
            var link = request.Link ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(link))
            {
                var existingLink = await this.directoryEntryRepository.GetByLinkAsync(link);
                if (existingLink != null)
                {
                    return existingLink.DirectoryEntryId;
                }

                var linkVariation = link.EndsWith("/") ? link[..^1] : $"{link}/";
                var existingLinkVariation1 = await this.directoryEntryRepository.GetByLinkAsync(linkVariation);
                if (existingLinkVariation1 != null)
                {
                    return existingLinkVariation1.DirectoryEntryId;
                }
            }

            var name = request.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                var existingName = await this.directoryEntryRepository.GetByNameAsync(name);
                if (existingName != null)
                {
                    return existingName.DirectoryEntryId;
                }
            }

            return null;
        }

        private async Task SetSelectSubCategoryViewBag()
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

        private async Task<SubmissionPreviewModel> GetSubmissionPreviewAsync(Submission submission)
        {
            var link2Name = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link2Name);
            var link3Name = await this.cacheHelper.GetSnippetAsync(SiteConfigSetting.Link3Name);

            if (submission.SubCategoryId != null)
            {
                submission.SubCategory = await this.subCategoryRepository.GetByIdAsync(submission.SubCategoryId.Value);
            }

            var tagsList = (submission.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                           .Select(t => t.Trim())
                           .Where(t => t.Length > 0)
                           .ToList();

            var subcatName = submission.SubCategory != null
                ? $"{submission.SubCategory.Category?.Name} > {submission.SubCategory.Name}"
                : "No Subcategory Assigned";

            return new SubmissionPreviewModel
            {
                DirectoryEntryViewModel = new DirectoryEntryViewModel
                {
                    DateOption = DisplayFormatting.Enums.DateDisplayOption.NotDisplayed,
                    IsSponsored = false,
                    Link2Name = link2Name,
                    Link3Name = link3Name,
                    Link = submission.Link,
                    Name = submission.Name,
                    Contact = submission.Contact,
                    Description = submission.Description,
                    DirectoryEntryId = submission.DirectoryEntryId ?? 0,
                    DirectoryStatus = (submission.DirectoryStatus == null || submission.DirectoryStatus == DirectoryStatus.Unknown)
                            ? DirectoryStatus.Admitted
                            : submission.DirectoryStatus.Value,
                    Link2 = submission.Link2,
                    Link3 = submission.Link3,
                    Location = submission.Location,
                    Note = submission.Note,
                    Processor = submission.Processor,
                    SubCategoryId = submission.SubCategoryId,
                    Tags = tagsList,
                    CountryCode = submission.CountryCode,
                    FoundedDate = submission.FoundedDate,
                },
                SubmissionId = submission.SubmissionId,
                NoteToAdmin = submission.NoteToAdmin,
                SubcategoryName = subcatName,
                RelatedLinks = submission.RelatedLinks.Take(MaxLinks).ToList(),
            };
        }

        private async Task<IActionResult> CreateSubmission(SubmissionRequest model)
        {
            if (!await this.HasChangesAsync(model) &&
                string.IsNullOrWhiteSpace(model.NoteToAdmin))
            {
                return this.RedirectToAction("Success", "Submission");
            }

            if ((model.SubCategoryId == null || model.SubCategoryId == 0) &&
                string.IsNullOrWhiteSpace(model.SuggestedSubCategory))
            {
                this.ModelState.AddModelError(string.Empty, "You must select a subcategory or supply a suggested one.");
                await this.LoadSubCategories();

                return this.View("SubmitEdit", model);
            }

            var existingLinkSubmission = await this.submissionRepository.GetByLinkAndStatusAsync(model.Link ?? string.Empty);

            if (existingLinkSubmission != null)
            {
                this.ModelState.AddModelError(string.Empty, "There is already a pending submission for this link.");
                await this.LoadSubCategories();

                return this.View("SubmitEdit", model);
            }

            var submissionModel = this.FormatSubmissionRequest(model);
            var submissionId = model.SubmissionId;

            if (submissionId == null)
            {
                var existingDirectoryEntryId = await this.GetExistingEntryAsync(model);

                if (existingDirectoryEntryId != null)
                {
                    await this.AssignExistingProperties(submissionModel, existingDirectoryEntryId.Value);
                }

                submissionModel.DirectoryEntryId = existingDirectoryEntryId;
                var createdSub = await this.submissionRepository.CreateAsync(submissionModel);
                submissionId = createdSub.SubmissionId;
            }
            else
            {
                var existingSubmission = await this.submissionRepository.GetByIdAsync(submissionId.Value);

                if (existingSubmission == null)
                {
                    return this.BadRequest(Constants.StringConstants.SubmissionDoesNotExist);
                }

                await this.UpdateSubmission(submissionModel, existingSubmission);
            }

            return this.RedirectToAction(
                "Preview",
                "Submission",
                new
                {
                    id = submissionId
                });
        }

        private async Task PopulateCountryDropDownList(object? selectedId = null)
        {
            // Get the dictionary of countries from the helper.
            var countries = CountryHelper.GetCountries();

            // Order and materialize into a new dictionary
            countries = countries
                .OrderBy(x => x.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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

        private async Task AssignExistingProperties(Submission submissionModel, int existingDirectoryEntryId)
        {
            var existingDirectoryEntry = await this.directoryEntryRepository.GetByIdAsync(existingDirectoryEntryId);

            if (existingDirectoryEntry != null)
            {
                // they are submitting a listing that is an override, not an edit, copy the status from the existing listing
                submissionModel.DirectoryStatus = existingDirectoryEntry.DirectoryStatus;
            }
        }

        private async Task LoadDropDowns()
        {
            await this.LoadSubCategories();
            await this.PopulateCountryDropDownList();
        }

        private async Task CreateDirectoryEntry(Submission model)
        {
            if (model.SubCategoryId == null)
            {
                throw new NullReferenceException(nameof(model.SubCategoryId));
            }

            var status = (DirectoryStatus)(model.DirectoryStatus == null ? DirectoryStatus.Admitted : model.DirectoryStatus);

            await this.directoryEntryRepository.CreateAsync(
                new DirectoryEntry
                {
                    DirectoryEntryKey = StringHelpers.UrlKey(model.Name ?? string.Empty),
                    Name = (model.Name ?? string.Empty).Trim(),
                    Link = (model.Link ?? string.Empty).Trim(),
                    Link2 = model.Link2?.Trim(),
                    Link3 = model.Link3?.Trim(),
                    Description = model.Description?.Trim(),
                    Location = model.Location?.Trim(),
                    Processor = model.Processor?.Trim(),
                    Note = model.Note?.Trim(),
                    Contact = model.Contact?.Trim(),
                    DirectoryStatus = status,
                    SubCategoryId = model.SubCategoryId.Value,
                    CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty,
                    CountryCode = model.CountryCode,
                    PgpKey = model.PgpKey?.Trim(),
                    ProofLink = model.ProofLink?.Trim(),
                    FoundedDate = model.FoundedDate,
                });
        }

        private async Task UpdateDirectoryEntry(Submission model)
        {
            if (model.DirectoryEntryId == null)
            {
                return;
            }

            if (model.SubCategoryId == null)
            {
                throw new NullReferenceException(nameof(model.SubCategoryId));
            }

            var existing = await this.directoryEntryRepository.GetByIdAsync(model.DirectoryEntryId.Value) ??
                                    throw new Exception("Submission has a directory entry id, but the entry does not exist.");
            existing.DirectoryEntryKey = StringHelpers.UrlKey(model.Name ?? string.Empty).UrlKey();
            existing.Name = (model.Name ?? string.Empty).Trim();
            existing.Link = (model.Link ?? string.Empty).Trim();
            existing.Link2 = model.Link2?.Trim();
            existing.Link3 = model.Link3?.Trim();
            existing.Description = model.Description?.Trim();
            existing.Location = model.Location?.Trim();
            existing.Processor = model.Processor?.Trim();
            existing.Note = model.Note?.Trim();
            existing.Contact = model.Contact?.Trim();
            existing.CountryCode = model.CountryCode;
            existing.PgpKey = model.PgpKey?.Trim();
            existing.ProofLink = model.ProofLink?.Trim();
            existing.FoundedDate = model.FoundedDate;

            if (model.DirectoryStatus != null)
            {
                existing.DirectoryStatus = model.DirectoryStatus.Value;
            }

            existing.SubCategoryId = model.SubCategoryId.Value;
            existing.UpdatedByUserId = this.userManager.GetUserId(this.User);

            await this.directoryEntryRepository.UpdateAsync(existing);
        }

        private Submission FormatSubmissionRequest(SubmissionRequest model)
        {
            TryParseFoundedDateParts(model.FoundedYear, model.FoundedMonth, model.FoundedDay, out var foundedDate, out _);

            var ipAddress = this.HttpContext.GetRemoteIpIfEnabled();
            var relatedLinks = NormalizeLinks(
               new[] { model.RelatedLink1, model.RelatedLink2, model.RelatedLink3 },
               MaxLinks);

            var submission = new Submission
            {
                SubmissionStatus = SubmissionStatus.Preview,
                Name = (model.Name ?? string.Empty).Trim(),
                Link = (model.Link ?? string.Empty).Trim(),
                Link2 = (model.Link2 ?? string.Empty).Trim(),
                Link3 = (model.Link3 ?? string.Empty).Trim(),
                ProofLink = (model.ProofLink ?? string.Empty).Trim(),
                VideoLink = (model.VideoLink ?? string.Empty).Trim(),
                Description = (model.Description ?? string.Empty).Trim(),
                Location = (model.Location ?? string.Empty).Trim(),
                Processor = (model.Processor ?? string.Empty).Trim(),
                Note = (model.Note ?? string.Empty).Trim(),
                Contact = (model.Contact ?? string.Empty).Trim(),
                SuggestedSubCategory = (model.SuggestedSubCategory ?? string.Empty).Trim(),
                SubCategoryId = (model.SubCategoryId == 0) ? null : model.SubCategoryId,
                IpAddress = ipAddress,
                DirectoryEntryId = (model.DirectoryEntryId == 0) ? null : model.DirectoryEntryId,
                DirectoryStatus = model.DirectoryStatus ?? DirectoryStatus.Unknown,
                NoteToAdmin = model.NoteToAdmin,
                Tags = model.Tags?.Trim(),
                CountryCode = model.CountryCode,
                PgpKey = (model.PgpKey ?? string.Empty).Trim(),
                SelectedTagIdsCsv = model.SelectedTagIdsCsv,
                FoundedDate = foundedDate,
            };

            submission.RelatedLinks = relatedLinks;

            return submission;
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

        private async Task UpdateSubmission(Submission submissionModel, Submission existingSubmission)
        {
            existingSubmission.SubmissionStatus = submissionModel.SubmissionStatus;
            existingSubmission.Location = submissionModel.Location;
            existingSubmission.DirectoryStatus = submissionModel.DirectoryStatus;
            existingSubmission.DirectoryEntryId = submissionModel.DirectoryEntryId;
            existingSubmission.Contact = submissionModel.Contact;
            existingSubmission.Description = submissionModel.Description;
            existingSubmission.IpAddress = submissionModel.IpAddress;
            existingSubmission.Link = submissionModel.Link;
            existingSubmission.Link2 = submissionModel.Link2;
            existingSubmission.Link3 = submissionModel.Link3;
            existingSubmission.Name = submissionModel.Name;
            existingSubmission.Note = submissionModel.Note;
            existingSubmission.NoteToAdmin = submissionModel.NoteToAdmin;
            existingSubmission.Processor = submissionModel.Processor;
            existingSubmission.SubCategoryId = submissionModel.SubCategoryId;
            existingSubmission.SuggestedSubCategory = submissionModel.SuggestedSubCategory;
            existingSubmission.CountryCode = submissionModel.CountryCode;
            existingSubmission.PgpKey = submissionModel.PgpKey;
            existingSubmission.ProofLink = submissionModel.ProofLink;
            existingSubmission.VideoLink = submissionModel.VideoLink;
            existingSubmission.SelectedTagIdsCsv = submissionModel.SelectedTagIdsCsv;
            existingSubmission.RelatedLinks = submissionModel.RelatedLinks;
            existingSubmission.FoundedDate = submissionModel.FoundedDate;

            await this.submissionRepository.UpdateAsync(existingSubmission);
        }

        private async Task LoadAllTagsForCheckboxesAsync()
        {
            var tags = await this.tagRepo.ListAllAsync();
            this.ViewBag.AllTags = tags
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new TagOptionVm { TagId = t.TagId, Name = t.Name })
                .ToList();
        }

        private static List<int> ParseCsvIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new List<int>();
            }

            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }
        private static List<string> NormalizeLinks(IEnumerable<string?>? links, int max)
        {
            return (links ?? Array.Empty<string?>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        private static HashSet<int> ParseIdsCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new HashSet<int>();
            }

            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();
        }

        private async Task<bool> HasChangesAsync(SubmissionRequest model)
        {
            if (model.DirectoryEntryId == null)
            {
                return true;
            }

            var existingEntry = await this.directoryEntryRepository.GetByIdAsync(model.DirectoryEntryId.Value);

            if (existingEntry == null)
            {
                return true;
            }

            TryParseFoundedDateParts(model.FoundedYear, model.FoundedMonth, model.FoundedDay, out var requestedFounded, out _);

            if (existingEntry.FoundedDate != requestedFounded)
            {
                return true;
            }

            static string Norm(string? s)
            {
                return (s ?? string.Empty).Trim();
            }

            static string NormCountry(string? s)
            {
                return (s ?? string.Empty).Trim().ToUpperInvariant();
            }

            static string NormPgp(string? s)
            {
                return (s ?? string.Empty).Trim();
            }

            if (!string.Equals(Norm(existingEntry.Contact), Norm(model.Contact), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Description), Norm(model.Description), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Link), Norm(model.Link), StringComparison.Ordinal))
            {
                return true;
            }

            // Tor / I2P / alternate links — unchanged meaning
            if (!string.Equals(Norm(existingEntry.Link2), Norm(model.Link2), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Link3), Norm(model.Link3), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.ProofLink), Norm(model.ProofLink), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.VideoLink), Norm(model.VideoLink), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Location), Norm(model.Location), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Name), Norm(model.Name), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Note), Norm(model.Note), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(Norm(existingEntry.Processor), Norm(model.Processor), StringComparison.Ordinal))
            {
                return true;
            }

            var requestedSubCatId = (model.SubCategoryId.HasValue && model.SubCategoryId.Value > 0)
                ? model.SubCategoryId.Value
                : 0;

            if (existingEntry.SubCategoryId != requestedSubCatId)
            {
                return true;
            }

            // Suggested status — only count if explicitly set to a real value
            if (model.DirectoryStatus.HasValue && model.DirectoryStatus.Value != DirectoryStatus.Unknown)
            {
                if (existingEntry.DirectoryStatus != model.DirectoryStatus.Value)
                {
                    return true;
                }
            }

            if (!string.Equals(NormCountry(existingEntry.CountryCode), NormCountry(model.CountryCode), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(NormPgp(existingEntry.PgpKey), NormPgp(model.PgpKey), StringComparison.Ordinal))
            {
                return true;
            }

            var selectedIds = ParseIdsCsv(model.SelectedTagIdsCsv);

            var existingTags = await this.entryTagRepo.GetTagsForEntryAsync(existingEntry.DirectoryEntryId);
            var existingIds = existingTags
                .Select(t => t.TagId)
                .ToHashSet();

            if (!existingIds.SetEquals(selectedIds))
            {
                return true;
            }

            // ----- Submission-only fields (not on DirectoryEntry) -----

            var incomingRelated = NormalizeLinks(
                new[] { model.RelatedLink1, model.RelatedLink2, model.RelatedLink3 },
                MaxLinks);

            // If editing an existing *preview submission*, compare against that submission’s stored related links.
            // If this is a brand new submission (no SubmissionId), then any provided related links count as a change.
            if (model.SubmissionId.HasValue && model.SubmissionId.Value > 0)
            {
                var existingSubmission = await this.submissionRepository.GetByIdAsync(model.SubmissionId.Value);
                var existingRelated = existingSubmission?.RelatedLinks ?? new List<string>();

                // compare as sets (order-insensitive)
                var incomingSet = incomingRelated.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingSet = existingRelated.ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!incomingSet.SetEquals(existingSet))
                {
                    return true;
                }
            }
            else
            {
                if (incomingRelated.Count > 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(Norm(model.NoteToAdmin)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Norm(model.NoteToAdmin)))
            {
                return true;
            }

            return false;
        }
    }
}