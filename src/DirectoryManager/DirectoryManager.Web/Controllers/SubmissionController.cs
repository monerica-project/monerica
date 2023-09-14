using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    // Controllers/SubmissionController.cs

    public class SubmissionController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly ISubCategoryRepository _subCategoryRepository;
        private readonly IDirectoryEntryRepository _directoryEntryRepository;

        public SubmissionController
            (UserManager<ApplicationUser> userManager,
            ISubmissionRepository submissionRepository,
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            _userManager = userManager;
            _submissionRepository = submissionRepository;
            _subCategoryRepository = subCategoryRepository;
            _directoryEntryRepository = directoryEntryRepository;
        }

        [AllowAnonymous]
        [HttpGet("submit")]
        public async Task<IActionResult> CreateAsync()
        {
            var subCategories = (await _subCategoryRepository.GetAllAsync())
                .OrderBy(sc => sc.Category.Name)
                .ThenBy(sc => sc.Name)
                .Select(sc => new
                {
                    sc.Id,
                    DisplayName = $"{sc.Category.Name} > {sc.Name}"
                })
                .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            ViewBag.SubCategories = subCategories;

            return View();
        }

        [AllowAnonymous]
        [HttpGet("submission/submitedit/{id}")]
        public async Task<IActionResult> SubmitEdit(int id)
        {
            var directoryEntry = await _directoryEntryRepository.GetByIdAsync(id);

            var subCategories = (await _subCategoryRepository.GetAllAsync())
              .OrderBy(sc => sc.Category.Name)
              .ThenBy(sc => sc.Name)
              .Select(sc => new
              {
                  sc.Id,
                  DisplayName = $"{sc.Category.Name} > {sc.Name}"
              })
              .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            ViewBag.SubCategories = subCategories;

            if (directoryEntry == null) return NotFound();

            var model = new SubmissionRequest()
            {
                Contact = directoryEntry.Contact,
                Description = (directoryEntry.Description == null) ? string.Empty : directoryEntry.Description,
                Link = directoryEntry.Link,
                Link2 =  (directoryEntry.Link2 == null) ? string.Empty : directoryEntry.Link2,
                Location = directoryEntry.Location,
                Name = directoryEntry.Name,
                Note = directoryEntry.Note,
                Processor = directoryEntry.Processor,
                SubCategoryId = directoryEntry.SubCategoryId,
                DirectoryEntryId = directoryEntry.Id,
                DirectoryStatus = directoryEntry.DirectoryStatus
            };
            return View(model);
        }

        [AllowAnonymous]
        [HttpGet("submission/findexisting")]
        public async Task<IActionResult> FindExisting(int? subCategoryId = null)
        {
            var entries = await _directoryEntryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory.Id == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            ViewBag.SubCategories = (await _subCategoryRepository.GetAllAsync())
                                    .OrderBy(sc => sc.Category.Name)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return View(entries);
        }

        [AllowAnonymous]
        [HttpPost("submit")]
        public async Task<IActionResult> Create(SubmissionRequest model)
        {
            if (ModelState.IsValid)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                if (!await HasChangesAsync(model))
                {
                    return RedirectToAction("Success", "Submission");
                }

                var submission = new Submission
                {
                    SubmissionStatus = Data.Enums.SubmissionStatus.Pending,
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

                await _submissionRepository.AddAsync(submission);

                return RedirectToAction("Success", "Submission"); 
            }

            return View(model);
        }

        private async Task<bool> HasChangesAsync(SubmissionRequest model)
        {
            if (model.DirectoryEntryId == null)
            {
                return true;
            }

            var existingEntry = await _directoryEntryRepository.GetByIdAsync(model.DirectoryEntryId.Value);

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

        [Authorize]
        [HttpGet("submission/index")]
        public async Task<IActionResult> Index()
        {
            var submissions = await _submissionRepository.GetAllAsync();
            return View(submissions); // This will display the list of submissions
        }

        [Authorize]
        [HttpGet("submission/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var submission = await _submissionRepository.GetByIdAsync(id);
            
            var subCategories = (await _subCategoryRepository.GetAllAsync())
              .OrderBy(sc => sc.Category.Name)
              .ThenBy(sc => sc.Name)
              .Select(sc => new
              {
                  sc.Id,
                  DisplayName = $"{sc.Category.Name} > {sc.Name}"
              })
              .ToList();

            subCategories.Insert(0, new { Id = 0, DisplayName = "Please select a category" });

            ViewBag.SubCategories = subCategories;

            if (submission == null) return NotFound();

            // Convert the submission to a ViewModel if necessary, or use the model directly
            return View(submission);
        }


        [Authorize]
        [HttpGet("submission/delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _submissionRepository.DeleteAsync(id);
 
            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        [HttpGet("submission/success")]
        public IActionResult Success()
        {
            return View("Success");
        }

        [Authorize]
        [HttpPost("submission/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Submission model)
        {
            if (ModelState.IsValid)
            {
                var submission = await _submissionRepository.GetByIdAsync(id);

                if (submission == null) return NotFound();

                if (submission.SubCategoryId == null ||
                    submission.SubCategoryId == 0)
                {
                    throw new Exception("Submission does not have a subcategory");
                }

                if (submission.SubmissionStatus == Data.Enums.SubmissionStatus.Pending &&
                    model.SubmissionStatus == Data.Enums.SubmissionStatus.Approved)
                {
                    // it's now approved
                    await _directoryEntryRepository.CreateAsync(
                        new DirectoryEntry
                        {
                            Name = model.Name.Trim(),
                            Link = model.Link.Trim(),
                            Description = model.Description.Trim(),
                            Location = model.Location?.Trim(),
                            Processor = model.Processor?.Trim(),
                            Note = model.Note?.Trim(),
                            Contact = model.Contact?.Trim(),
                            DirectoryStatus = Data.Enums.DirectoryStatus.Admitted,
                            SubCategoryId = model.SubCategoryId,
                            CreatedByUserId = _userManager.GetUserId(User)
                });
                }

                submission.SubmissionStatus = model.SubmissionStatus;

                await _submissionRepository.UpdateAsync(submission);

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

    }
}
