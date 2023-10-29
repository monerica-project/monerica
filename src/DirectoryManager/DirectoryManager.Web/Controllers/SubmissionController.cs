using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class SubmissionController : BaseController
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ISubmissionRepository submissionRepository;
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IDirectoryEntriesAuditRepository auditRepository;
        private readonly IMemoryCache cache;

        public SubmissionController(
            UserManager<ApplicationUser> userManager,
            ISubmissionRepository submissionRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            IDirectoryEntriesAuditRepository auditRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.userManager = userManager;
            this.submissionRepository = submissionRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.auditRepository = auditRepository;
            this.cache = cache;
        }

        [AllowAnonymous]
        [HttpGet("submit")]
        public async Task<IActionResult> CreateAsync()
        {
            await this.LoadSubCategories();

            return this.View();
        }

        [AllowAnonymous]
        [HttpPost("submit")]
        public async Task<IActionResult> Create(SubmissionRequest model)
        {
            if (!WebRequestHelper.IsValidUrl(model.Link))
            {
                this.ModelState.AddModelError("Link", "The link is not a valid URL.");
            }

            if (this.ContainsScriptTag(model))
            {
                return this.RedirectToAction("Success", "Submission");
            }

            if (this.ModelState.IsValid)
            {
                var ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString();

                if (!await this.HasChangesAsync(model))
                {
                    return this.RedirectToAction("Success", "Submission");
                }

                var submission = new Submission
                {
                    SubmissionStatus = SubmissionStatus.Pending,
                    Name = model.Name.Trim(),
                    Link = (model.Link == null) ? string.Empty : model.Link.Trim(),
                    Link2 = (model.Link2 == null) ? string.Empty : model.Link2.Trim(),
                    Description = (model.Description == null) ? string.Empty : model.Description.Trim(),
                    Location = (model.Location == null) ? string.Empty : model.Location.Trim(),
                    Processor = (model.Processor == null) ? string.Empty : model.Processor.Trim(),
                    Note = (model.Note == null) ? string.Empty : model.Note.Trim(),
                    Contact = (model.Contact == null) ? string.Empty : model.Contact.Trim(),
                    SuggestedSubCategory = (model.SuggestedSubCategory == null) ? string.Empty : model.SuggestedSubCategory.Trim(),
                    SubCategoryId = (model.SubCategoryId == 0) ? null : model.SubCategoryId,
                    IpAddress = ipAddress,
                    DirectoryEntryId = (model.DirectoryEntryId == 0) ? null : model.DirectoryEntryId,
                    DirectoryStatus = (model.DirectoryStatus == null) ? DirectoryStatus.Unknown : model.DirectoryStatus
                };

                await this.submissionRepository.AddAsync(submission);

                return this.RedirectToAction("Success", "Submission");
            }
            else
            {
                await this.LoadSubCategories();
            }

            return this.View("SubmitEdit", model);
        }

        [AllowAnonymous]
        [HttpGet("submission/submitedit/{id}")]
        public async Task<IActionResult> SubmitEdit(int id)
        {
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            var subCategories = (await this.subCategoryRepository.GetAllAsync())
              .OrderBy(sc => sc.Category.Name)
              .ThenBy(sc => sc.Name)
              .Select(sc => new
              {
                  sc.Id,
                  DisplayName = $"{sc.Category.Name} > {sc.Name}"
              })
              .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            this.ViewBag.SubCategories = subCategories;

            if (directoryEntry == null)
            {
                return this.NotFound();
            }

            var model = new SubmissionRequest()
            {
                Contact = directoryEntry.Contact,
                Description = directoryEntry.Description ?? string.Empty,
                Link = directoryEntry.Link,
                Link2 = directoryEntry.Link2 ?? string.Empty,
                Location = directoryEntry.Location,
                Name = directoryEntry.Name,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
                DirectoryEntryId = directoryEntry.Id,
                DirectoryStatus = directoryEntry.DirectoryStatus
            };
            return this.View(model);
        }

        [AllowAnonymous]
        [HttpGet("submission/findexisting")]
        public async Task<IActionResult> FindExisting(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory?.Id == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllAsync())
                                    .OrderBy(sc => sc.Category.Name)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return this.View(entries);
        }

        [HttpGet("submission/audit/{entryId}")]
        public async Task<IActionResult> AuditAync(int entryId)
        {
            var audits = await this.auditRepository.GetAuditsForEntryAsync(entryId);
            return this.View("Audit", audits);
        }

        [Authorize]
        [HttpGet("submission/index")]
        public async Task<IActionResult> Index(int? page, int pageSize = 10)
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
        public async Task<IActionResult> Edit(int id)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);

            if (submission != null &&
                submission.DirectoryEntryId != null)
            {
                var existing = await this.directoryEntryRepository.GetByIdAsync(submission.DirectoryEntryId.Value);
                if (existing != null)
                {
                    this.ViewBag.Differences = ModelComparisionHelpers.CompareEntries(existing, submission);
                }
            }

            var subCategories = (await this.subCategoryRepository.GetAllAsync())
              .OrderBy(sc => sc.Category.Name)
              .ThenBy(sc => sc.Name)
              .Select(sc => new
              {
                  sc.Id,
                  DisplayName = $"{sc.Category.Name} > {sc.Name}"
              })
              .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            this.ViewBag.SubCategories = subCategories;

            if (submission == null)
            {
                return this.NotFound();
            }

            // Convert the submission to a ViewModel if necessary, or use the model directly
            return this.View(submission);
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

        [Authorize]
        [HttpPost("submission/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Submission model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var submission = await this.submissionRepository.GetByIdAsync(id);

            if (submission == null)
            {
                return this.NotFound();
            }

            if (model.SubCategoryId == null ||
                model.SubCategoryId == 0)
            {
                throw new Exception("Submission does not have a subcategory");
            }

            if (submission.SubmissionStatus == SubmissionStatus.Pending &&
                model.SubmissionStatus == SubmissionStatus.Approved)
            {
                if (model.DirectoryEntryId == null)
                {
                    // it's now approved
                    await this.directoryEntryRepository.CreateAsync(
                        new DirectoryEntry
                        {
                            Name = model.Name.Trim(),
                            Link = model.Link.Trim(),
                            Link2 = model.Link2?.Trim(),
                            Description = model.Description?.Trim(),
                            Location = model.Location?.Trim(),
                            Processor = model.Processor?.Trim(),
                            Note = model.Note?.Trim(),
                            Contact = model.Contact?.Trim(),
                            DirectoryStatus = Data.Enums.DirectoryStatus.Admitted,
                            SubCategoryId = model.SubCategoryId,
                            CreatedByUserId = this.userManager.GetUserId(this.User) ?? string.Empty
                        });
                }
                else
                {
                    var existing = await this.directoryEntryRepository.GetByIdAsync(model.DirectoryEntryId.Value) ??
                        throw new Exception("Submission has a directory entry id, but the entry does not exist.");
                    existing.Name = model.Name.Trim();
                    existing.Link = model.Link.Trim();
                    existing.Link2 = model.Link2?.Trim();
                    existing.Description = model.Description?.Trim();
                    existing.Location = model.Location?.Trim();
                    existing.Processor = model.Processor?.Trim();
                    existing.Note = model.Note?.Trim();
                    existing.Contact = model.Contact?.Trim();

                    if (model.DirectoryStatus != null)
                    {
                        existing.DirectoryStatus = model.DirectoryStatus.Value;
                    }

                    existing.SubCategoryId = model.SubCategoryId;
                    existing.UpdatedByUserId = this.userManager.GetUserId(this.User);

                    await this.directoryEntryRepository.UpdateAsync(existing);
                }

                this.ClearCachedItems();
            }

            submission.SubmissionStatus = model.SubmissionStatus;

            await this.submissionRepository.UpdateAsync(submission);

            return this.RedirectToAction(nameof(this.Index));
        }

        private async Task LoadSubCategories()
        {
            var subCategories = (await this.subCategoryRepository.GetAllAsync())
                .OrderBy(sc => sc.Category.Name)
                .ThenBy(sc => sc.Name)
                .Select(sc => new
                {
                    sc.Id,
                    DisplayName = $"{sc.Category.Name} > {sc.Name}"
                })
                .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            this.ViewBag.SubCategories = subCategories;
        }

        private bool ContainsScriptTag(SubmissionRequest model)
        {
            var properties = model.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(model) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var decodedValue = System.Net.WebUtility.HtmlDecode(value);
                        var normalizedValue = System.Text.RegularExpressions.Regex.Replace(decodedValue, @"\s+", " ").ToLower();
                        if (normalizedValue.Contains("<script") || normalizedValue.Contains("< script") || normalizedValue.Contains("&lt;script&gt;"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
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

            return false;
        }
    }
}