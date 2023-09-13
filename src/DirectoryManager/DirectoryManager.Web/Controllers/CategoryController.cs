using Microsoft.AspNetCore.Mvc;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Identity;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICategoryRepository _categoryRepository;

        public CategoryController(
                    UserManager<ApplicationUser> userManager, 
                    ICategoryRepository categoryRepository)
        {
            _userManager = userManager;
            _categoryRepository = categoryRepository;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var categories = await _categoryRepository.GetAllAsync();

            // Assuming categories is an IEnumerable<Category>, you can use Linq to skip and take for paging
            var pagedCategories = categories.Skip((page - 1) * pageSize).Take(pageSize);
            return View(pagedCategories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            category.CreatedByUserId = _userManager.GetUserId(User);
            category.Name = category.Name.Trim();
            category.CategoryKey = TextHelpers.UrlKey(category.Name);
            category.Description = category.Description?.Trim();
            category.Note = category.Note?.Trim();

            await _categoryRepository.CreateAsync(category);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
                return NotFound();

            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Category category)
        {
            var existingCategory = await _categoryRepository.GetByIdAsync(category.Id);

            if (existingCategory == null)
                return NotFound();

            existingCategory.Name = category.Name.Trim();
            existingCategory.CategoryKey = TextHelpers.UrlKey(category.Name);
            existingCategory.Description = category.Description?.Trim();
            existingCategory.Note = category.Note?.Trim();
            existingCategory.UpdatedByUserId = _userManager.GetUserId(User);

            await _categoryRepository.UpdateAsync(existingCategory);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            await _categoryRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}