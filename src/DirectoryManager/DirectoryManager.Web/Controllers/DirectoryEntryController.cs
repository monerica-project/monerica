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

    public DirectoryEntryController(
        UserManager<ApplicationUser> userManager,
        IDirectoryEntryRepository entryRepository, 
        ISubCategoryRepository subCategoryRepository, 
        ICategoryRepository categoryRepository)
    {
        _userManager = userManager;
        _entryRepository = entryRepository;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
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
        ViewBag.SubCategories = await _subCategoryRepository.GetAllAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(DirectoryEntry entry)
    {
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
        existingEntry.UpdatedByUserId = _userManager.GetUserId(User);
        existingEntry.SubCategoryId = entry.SubCategoryId;
        existingEntry.Link = entry.Link;
        existingEntry.Name = entry.Name;
        await _entryRepository.UpdateAsync(existingEntry);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        await _entryRepository.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
