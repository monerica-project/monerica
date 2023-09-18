using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _context.Categories
                                 .OrderBy(x => x.Name)
                                 .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task CreateAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var categoryToDelete = await _context.Categories.FindAsync(id);
            if (categoryToDelete != null)
            {
                _context.Categories.Remove(categoryToDelete);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Category> GetByNameAsync(string name)
        {
            return await _context.Categories.FirstOrDefaultAsync(sc => sc.Name == name) 
                ?? throw new Exception("Category not found");
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            var activeCategoryIds = await _context.DirectoryEntries
               .Where(entry =>
                    entry.DirectoryStatus != DirectoryStatus.Removed && entry.DirectoryStatus != DirectoryStatus.Unknown)
               .Where(entry => entry.SubCategory != null) // Ensure SubCategory is not null before accessing its properties
               .Select(entry => entry.SubCategory!.CategoryId) // Now it's safe to access CategoryId
               .Distinct()
               .ToListAsync();

            var activeCategories = await _context.Categories
                .Where(category => activeCategoryIds.Contains(category.Id))
                .OrderBy(category => category.Name)
                .ToListAsync();

            return activeCategories;
        }
    }
}
