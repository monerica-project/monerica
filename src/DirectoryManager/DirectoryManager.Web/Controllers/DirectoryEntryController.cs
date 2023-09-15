using Microsoft.AspNetCore.Mvc;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

[Authorize]
public class DirectoryEntryController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDirectoryEntryRepository _entryRepository;
    private readonly ISubCategoryRepository _subCategoryRepository;
    private readonly ICategoryRepository _categoryRepository; // Assuming you have this for fetching categories
    private readonly IDirectoryEntriesAuditRepository _auditRepository;

    public DirectoryEntryController(
        UserManager<ApplicationUser> userManager,
        IDirectoryEntryRepository entryRepository, 
        ISubCategoryRepository subCategoryRepository, 
        ICategoryRepository categoryRepository,
     IDirectoryEntriesAuditRepository auditRepository
        )
    {
        _userManager = userManager;
        _entryRepository = entryRepository;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
        _auditRepository = auditRepository;
    }

    public async Task<IActionResult> Index(int? subCategoryId = null)
    {
        var entries = await _entryRepository.GetAllAsync();

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

    [HttpGet]
    public async Task<IActionResult> Create()
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

    [HttpPost]
    public async Task<IActionResult> Create(DirectoryEntry entry)
    {
        entry.CreatedByUserId = _userManager.GetUserId(User);
        entry.SubCategoryId = entry.SubCategoryId;
        entry.Link = entry.Link.Trim();
        entry.Link2 = entry.Link2?.Trim();
        entry.Name = entry.Name.Trim();
        entry.Description = entry.Description?.Trim();
        entry.Note = entry.Note?.Trim();
        entry.DirectoryStatus = entry.DirectoryStatus;
        entry.Contact = entry.Contact?.Trim();
        entry.Location = entry.Location?.Trim();
        entry.Processor = entry.Processor?.Trim();

        await _entryRepository.CreateAsync(entry);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _entryRepository.GetByIdAsync(id);
        if (entry == null)
            return NotFound();

        ViewBag.SubCategories = await _subCategoryRepository.GetAllAsync();
        return View(entry);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(DirectoryEntry entry)
    {
        var existingEntry = await _entryRepository.GetByIdAsync(entry.Id);

        entry.UpdatedByUserId = _userManager.GetUserId(User);
        existingEntry.SubCategoryId = entry.SubCategoryId;
        existingEntry.Link = entry.Link.Trim();
        existingEntry.Link2 = entry.Link2?.Trim();
        existingEntry.Name = entry.Name.Trim();
        existingEntry.Description = entry.Description?.Trim();
        existingEntry.Note = entry.Note?.Trim();
        existingEntry.DirectoryStatus = entry.DirectoryStatus;
        existingEntry.Contact = entry.Contact?.Trim();
        existingEntry.Location = entry.Location?.Trim();
        
        await _entryRepository.UpdateAsync(existingEntry);
        return RedirectToAction(nameof(Index));
    }

 
    [HttpGet("EntryAudits/{entryId}")]
    public async Task<IActionResult> EntryAudits(int entryId)
    {
        var audits = await _auditRepository.GetAuditsForEntryAsync(entryId);
        return View(audits);
    }


    public async Task<IActionResult> Delete(int id)
    {
        await _entryRepository.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

}
