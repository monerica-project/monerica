using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly IApplicationDbContext context;

        public CategoryRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await this.context.Categories
                                 .OrderBy(x => x.Name)
                                 .ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int categoryId)
        {
            return await this.context.Categories.FindAsync(categoryId);
        }

        public async Task CreateAsync(Category category)
        {
            await this.context.Categories.AddAsync(category);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            this.context.Categories.Update(category);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int categoryId)
        {
            var categoryToDelete = await this.context.Categories.FindAsync(categoryId);
            if (categoryToDelete != null)
            {
                this.context.Categories.Remove(categoryToDelete);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<Category> GetByNameAsync(string name)
        {
            return await this.context.Categories.FirstOrDefaultAsync(sc => sc.Name == name)
                ?? throw new Exception("Category not found");
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            var activeCategoryIds = await this.context.DirectoryEntries
               .Where(entry =>
                    entry.DirectoryStatus != DirectoryStatus.Removed && entry.DirectoryStatus != DirectoryStatus.Unknown)
               .Where(entry => entry.SubCategory != null) // Ensure SubCategory is not null before accessing its properties
               .Select(entry => entry.SubCategory!.CategoryId) // Now it's safe to access CategoryId
               .Distinct()
               .ToListAsync();

            var activeCategories = await this.context.Categories
                .Where(category => activeCategoryIds.Contains(category.CategoryId))
                .OrderBy(category => category.Name)
                .ToListAsync();

            return activeCategories;
        }
    }
}
