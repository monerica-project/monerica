using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubCategoryRepository : ISubCategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public SubCategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SubCategory>> GetAllAsync()
        {
            return await _context.SubCategories
                            .Include(e => e.Category)
                            .OrderBy(e => e.Category.Name)
                            .OrderBy(e => e.Name)
                            .ToListAsync();
        }

        public async Task<SubCategory> GetByIdAsync(int id)
        {
            return await _context.SubCategories.FindAsync(id);
        }

        public async Task CreateAsync(SubCategory subCategory)
        {
            await _context.SubCategories.AddAsync(subCategory);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubCategory subCategory)
        {
            _context.SubCategories.Update(subCategory);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var subCategoryToDelete = await _context.SubCategories.FindAsync(id);
            if (subCategoryToDelete != null)
            {
                _context.SubCategories.Remove(subCategoryToDelete);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<SubCategory> GetByNameAsync(string name)
        {
            return await _context.SubCategories.FirstOrDefaultAsync(sc => sc.Name == name);
        }

        public async Task<IEnumerable<SubCategory>> GetByCategoryAsync(int categoryId)
        {
            return await _context.SubCategories
                                 .Where(sc => sc.CategoryId == categoryId)
                                 .OrderBy(sc => sc.Name)
                                 .ToListAsync();
        }

        public async Task<IEnumerable<SubCategory>> GetActiveSubCategoriesAsync(int categoryId)
        {
            var activeSubCategories = await _context.SubCategories
                          .Where(subCategory => subCategory.CategoryId == categoryId &&
                              _context.DirectoryEntries
                                  .Any(entry => entry.SubCategoryId == subCategory.Id &&
                                                entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                                entry.DirectoryStatus != DirectoryStatus.Removed))
                          .OrderBy(subCategory => subCategory.Name)
                          .ToListAsync();

            return activeSubCategories;
        }
    }
}
