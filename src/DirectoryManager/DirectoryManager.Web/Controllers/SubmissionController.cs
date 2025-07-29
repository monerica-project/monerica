using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
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
            IDirectoryEntryTagRepository entryTagRepo)
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
            }

            await this.LoadDropDowns();

            return this.View(model);
        }

        [AllowAnonymous]
        [HttpPost("submit")]
        public async Task<IActionResult> Create(SubmissionRequest model)
        {
            if (!UrlHelper.IsValidUrl(model.Link))
            {
                this.ModelState.AddModelError("Link", "The link is not a valid URL.");
            }

            if (!string.IsNullOrWhiteSpace(model.Link2) && !UrlHelper.IsValidUrl(model.Link2))
            {
                this.ModelState.AddModelError("Link2", "The link 2 is not a valid URL.");
            }

            if (!string.IsNullOrWhiteSpace(model.Link3) && !UrlHelper.IsValidUrl(model.Link3))
            {
                this.ModelState.AddModelError("Link3", "The link 3 is not a valid URL.");
            }

            var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            if (ScriptValidation.ContainsScriptTag(model) ||
                this.blockedIPRepository.IsBlockedIp(ipAddress))
            {
                return this.RedirectToAction("Success", "Submission");
            }

            if (this.ModelState.IsValid)
            {
                return await this.CreateSubmission(model);
            }
            else
            {
                await this.LoadDropDowns();
            }

            return this.View("SubmitEdit", model);
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

        // SubmissionController.cs
        [AllowAnonymous]
        [HttpGet("submission/submitedit/{id}")]
        public async Task<IActionResult> SubmitEdit(int id)
        {
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);
            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            // load existing tags for this entry
            var entryTags = await this.entryTagRepo.GetTagsForEntryAsync(id);
            var tagCsv = string.Join(
                ", ",
                entryTags
                  .OrderBy(et => et.Name, StringComparer.OrdinalIgnoreCase)
                  .Select(et => et.Name));

            // map into your submission request VM
            var model = GetSubmissionRequestModel(directoryEntry);
            model.Tags = tagCsv;

            await this.SetSelectSubCategoryViewBag();
            await this.LoadDropDowns();

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
            var link2Name = this.cacheHelper.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheHelper.GetSnippet(SiteConfigSetting.Link3Name);
            var canonicalDomain = this.cacheHelper.GetSnippet(SiteConfigSetting.CanonicalDomain);
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
        public async Task<IActionResult> Review(int id)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            // If this is an edit of an existing entry, load its category/subcategory and tags
            if (submission.DirectoryEntryId.HasValue)
            {
                // Ensure SubCategory nav prop is populated
                if (submission.SubCategory == null && submission.SubCategoryId.HasValue)
                {
                    submission.SubCategory = await this.subCategoryRepository
                        .GetByIdAsync(submission.SubCategoryId.Value);
                }

                // Load current entry from DB
                var existing = await this.directoryEntryRepository
                    .GetByIdAsync(submission.DirectoryEntryId.Value);

                if (existing != null)
                {
                    // 1) load its current tags
                    var existingTags = await this.entryTagRepo
                        .GetTagsForEntryAsync(existing.DirectoryEntryId);

                    // 2) set submission.Tags so the UI shows them
                    existing.Tags = string.Join(
                        ", ",
                        existingTags
                            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                            .Select(t => t.Name));

                    // 3) compute the diff
                    this.ViewBag.Differences = ModelComparisonHelpers
                        .CompareEntries(existing, submission);
                }
            }

            await this.LoadDropDowns();
            await this.SetSelectSubCategoryViewBag();

            return this.View(submission);
        }

        [Authorize]
        [HttpPost("submission/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, Submission model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            if (model.DirectoryStatus == DirectoryStatus.Unknown)
            {
                throw new Exception($"Invalid directory status: {model.DirectoryStatus}");
            }

            // 1) load the submission
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            // 2) if we’re moving from Pending → Approved, create/update the DirectoryEntry
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
                    // create new entry
                    await this.CreateDirectoryEntry(model);
                    var created = await this.directoryEntryRepository.GetByLinkAsync(model.Link.Trim());
                    entryId = created?.DirectoryEntryId
                              ?? throw new Exception("Failed to locate newly created entry");
                }
                else
                {
                    // update existing
                    await this.UpdateDirectoryEntry(model);
                    entryId = model.DirectoryEntryId.Value;
                }

                // 3) parse & assign tags if any were supplied
                if (!string.IsNullOrWhiteSpace(model.Tags))
                {
                    // remove old tags
                    var oldTags = await this.entryTagRepo.GetTagsForEntryAsync(entryId);
                    foreach (var t in oldTags)
                    {
                        await this.entryTagRepo.RemoveTagAsync(entryId, t.TagId);
                    }

                    // split, normalize, up to 7 distinct tags
                    var names = model.Tags
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => t.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(7);

                    foreach (var name in names)
                    {
                        // get or create the Tag row
                        var tag = await this.tagRepo.GetByKeyAsync(name.UrlKey())
                               ?? await this.tagRepo.CreateAsync(name);

                        // link it
                        await this.entryTagRepo.AssignTagAsync(entryId, tag.TagId);
                    }
                }

                this.ClearCachedItems();
            }

            // 4) store the admin’s final status & tags on the submission record
            submission.SubmissionStatus = model.SubmissionStatus;
            submission.CountryCode = model.CountryCode;
            submission.Tags = model.Tags?.Trim();

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
        public async Task<IActionResult> ConfirmAsync(int submissionId)
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

            submission.SubmissionStatus = SubmissionStatus.Pending;

            await this.submissionRepository.UpdateAsync(submission);

            return this.View("Success");
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
                Name = directoryEntry.Name,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
                DirectoryEntryId = directoryEntry.DirectoryEntryId,
                DirectoryStatus = directoryEntry.DirectoryStatus,
                CountryCode = directoryEntry.CountryCode,
            };
        }

        private static SubmissionRequest GetSubmissionCreateModel(Submission submission)
        {
            return new SubmissionRequest()
            {
                SubCategoryId = (submission.SubCategoryId == null) ? null : submission.SubCategoryId,
                NoteToAdmin = submission.NoteToAdmin,
                Contact = submission.Contact,
                Description = submission.Description,
                DirectoryEntryId = submission.DirectoryEntryId,
                DirectoryStatus = submission.DirectoryStatus,
                Link = submission.Link,
                Link2 = submission.Link2,
                Link3 = submission.Link3,
                Location = submission.Location,
                Name = submission.Name,
                Note = submission.Note,
                Processor = submission.Processor,
                SuggestedSubCategory = submission.SuggestedSubCategory,
                Tags = submission.Tags
            };
        }

        private async Task<int?> GetExistingEntryAsync(SubmissionRequest request)
        {
            var link = request.Link;
            var existingLink = await this.directoryEntryRepository.GetByLinkAsync(link);

            if (existingLink != null)
            {
                return existingLink.DirectoryEntryId;
            }

            var linkVariation = link.EndsWith("/") ? link.Remove(link.Length - 1) : string.Format("{0}/", link);
            var existingLinkVariation1 = await this.directoryEntryRepository.GetByLinkAsync(linkVariation);

            if (existingLinkVariation1 != null)
            {
                return existingLinkVariation1.DirectoryEntryId;
            }

            var existingName = await this.directoryEntryRepository.GetByNameAsync(request.Name);

            if (existingName != null)
            {
                return existingName.DirectoryEntryId;
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
            var link2Name = this.cacheHelper.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheHelper.GetSnippet(SiteConfigSetting.Link3Name);

            if (submission.SubCategoryId != null)
            {
                submission.SubCategory = await this.subCategoryRepository.GetByIdAsync(submission.SubCategoryId.Value);
            }

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
                    DirectoryEntryId = (submission.DirectoryEntryId != null) ? submission.DirectoryEntryId.Value : 0,
                    DirectoryStatus = (submission.DirectoryStatus == null || submission.DirectoryStatus == DirectoryStatus.Unknown)
                            ? DirectoryStatus.Admitted :
                            submission.DirectoryStatus.Value,
                    Link2 = submission.Link2,
                    Link3 = submission.Link3,
                    Location = submission.Location,
                    Note = submission.Note,
                    Processor = submission.Processor,
                    SubCategoryId = submission.SubCategoryId,
                    Tags = submission?.Tags?.Split(",").ToList()
                },
                SubmissionId = submission.SubmissionId,
                NoteToAdmin = submission.NoteToAdmin,
                SubcategoryName = $"{submission?.SubCategory?.Category?.Name} > {submission?.SubCategory?.Name}",
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

            var existingLinkSubmission = await this.submissionRepository.GetByLinkAndStatusAsync(model.Link);

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
                var submission = await this.submissionRepository.CreateAsync(submissionModel);
                submissionId = submission.SubmissionId;
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

        private async Task PopulateCountryDropDownList(object selectedId = null)
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

        private async Task AssignExistingProperties(Submission submissionModel, int existingDirectoryEntryId)
        {
            var existingDirectoryEntry = await this.directoryEntryRepository.GetByIdAsync(existingDirectoryEntryId);

            if (existingDirectoryEntry != null)
            {
                // they are submitting a listing that is an override, not an edit, copy the status from the existing listing
                submissionModel.DirectoryStatus = existingDirectoryEntry?.DirectoryStatus;
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

            await this.directoryEntryRepository.CreateAsync(
                new DirectoryEntry
                {
                    DirectoryEntryKey = StringHelpers.UrlKey(model.Name),
                    Name = model.Name.Trim(),
                    Link = model.Link.Trim(),
                    Link2 = model.Link2?.Trim(),
                    Link3 = model.Link3?.Trim(),
                    Description = model.Description?.Trim(),
                    Location = model.Location?.Trim(),
                    Processor = model.Processor?.Trim(),
                    Note = model.Note?.Trim(),
                    Contact = model.Contact?.Trim(),
                    DirectoryStatus = DirectoryStatus.Admitted,
                    SubCategoryId = model.SubCategoryId.Value,
                    CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty
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
            existing.Name = model.Name.Trim();
            existing.Link = model.Link.Trim();
            existing.Link2 = model.Link2?.Trim();
            existing.Link3 = model.Link3?.Trim();
            existing.Description = model.Description?.Trim();
            existing.Location = model.Location?.Trim();
            existing.Processor = model.Processor?.Trim();
            existing.Note = model.Note?.Trim();
            existing.Contact = model.Contact?.Trim();

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
            var ipAddress = this.HttpContext.GetRemoteIpIfEnabled();

            var submission = new Submission
            {
                SubmissionStatus = SubmissionStatus.Preview,
                Name = model.Name.Trim(),
                Link = (model.Link == null) ? string.Empty : model.Link.Trim(),
                Link2 = (model.Link2 == null) ? string.Empty : model.Link2.Trim(),
                Link3 = (model.Link3 == null) ? string.Empty : model.Link3.Trim(),
                Description = (model.Description == null) ? string.Empty : model.Description.Trim(),
                Location = (model.Location == null) ? string.Empty : model.Location.Trim(),
                Processor = (model.Processor == null) ? string.Empty : model.Processor.Trim(),
                Note = (model.Note == null) ? string.Empty : model.Note.Trim(),
                Contact = (model.Contact == null) ? string.Empty : model.Contact.Trim(),
                SuggestedSubCategory = (model.SuggestedSubCategory == null) ? string.Empty : model.SuggestedSubCategory.Trim(),
                SubCategoryId = (model.SubCategoryId == 0) ? null : model.SubCategoryId,
                IpAddress = ipAddress,
                DirectoryEntryId = (model.DirectoryEntryId == 0) ? null : model.DirectoryEntryId,
                DirectoryStatus = (model.DirectoryStatus == null) ? DirectoryStatus.Unknown : model.DirectoryStatus,
                NoteToAdmin = model.NoteToAdmin,
                Tags = model.Tags?.Trim(),
                CountryCode = model.CountryCode
            };
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

            await this.submissionRepository.UpdateAsync(existingSubmission);
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

            if (existingEntry.Contact?.Trim() != model.Contact?.Trim())
            {
                return true;
            }

            if (existingEntry.Description?.Trim() != model.Description?.Trim())
            {
                return true;
            }

            if (existingEntry.Link?.Trim() != model.Link?.Trim())
            {
                return true;
            }

            if (existingEntry.Link2?.Trim() != model.Link2?.Trim())
            {
                return true;
            }

            if (existingEntry.Link3?.Trim() != model.Link3?.Trim())
            {
                return true;
            }

            if (existingEntry.Location?.Trim() != model.Location?.Trim())
            {
                return true;
            }

            if (existingEntry.Name?.Trim() != model.Name?.Trim())
            {
                return true;
            }

            if (existingEntry.Note?.Trim() != model.Note?.Trim())
            {
                return true;
            }

            if (existingEntry.Processor?.Trim() != model.Processor?.Trim())
            {
                return true;
            }

            if (existingEntry.SubCategoryId != model.SubCategoryId)
            {
                return true;
            }

            if (existingEntry.DirectoryStatus != model.DirectoryStatus)
            {
                return true;
            }

            if (existingEntry.Tags != model.Tags)
            {
                return true;
            }

            return false;
        }
    }
}