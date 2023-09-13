using Microsoft.AspNetCore.Mvc;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DirectoryManager.Web.Helpers;

[Authorize]
public class SubCategoryController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubCategoryRepository _subCategoryRepository;
    private readonly ICategoryRepository _categoryRepository; // Assuming you have this for fetching categories

    public SubCategoryController(
        UserManager<ApplicationUser> userManager,
        ISubCategoryRepository subCategoryRepository,
        ICategoryRepository categoryRepository)
    {
        _userManager = userManager;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId = null)
    {
        IEnumerable<SubCategory> subCategories;

        if (categoryId.HasValue)
        {
            subCategories = await _subCategoryRepository.GetAllAsync();
            subCategories = subCategories.Where(sc => sc.CategoryId == categoryId.Value);
        }
        else
        {
            subCategories = await _subCategoryRepository.GetAllAsync();
        }

        ViewBag.Categories = await _categoryRepository.GetAllAsync(); // For dropdown list

        return View(subCategories);
    }


    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _categoryRepository.GetAllAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(SubCategory subCategory)
    {
        subCategory.CreatedByUserId = _userManager.GetUserId(User);
        subCategory.SubCategoryKey = TextHelpers.UrlKey(subCategory.Name);
        subCategory.Name = subCategory.Name.Trim();
        subCategory.Description = subCategory.Description?.Trim();
        subCategory.Note = subCategory.Note?.Trim();

        await _subCategoryRepository.CreateAsync(subCategory);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var subCategory = await _subCategoryRepository.GetByIdAsync(id);
        if (subCategory == null)
            return NotFound();

        ViewBag.Categories = await _categoryRepository.GetAllAsync();
        return View(subCategory);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(SubCategory subCategory)
    {
        var existingSubCategory = await _subCategoryRepository.GetByIdAsync(subCategory.Id);

        existingSubCategory.Name = subCategory.Name.Trim();
        existingSubCategory.SubCategoryKey = TextHelpers.UrlKey(subCategory.Name.Trim());
        existingSubCategory.CategoryId = subCategory.CategoryId;
        existingSubCategory.Description = subCategory.Description?.Trim();
        existingSubCategory.Note = subCategory.Note?.Trim();
        existingSubCategory.UpdatedByUserId = _userManager.GetUserId(User);

        await _subCategoryRepository.UpdateAsync(existingSubCategory);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        await _subCategoryRepository.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
