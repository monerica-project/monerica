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

        public async Task<Category?> GetByKeyAsync(string categoryKey)
        {
            return await this.context.Categories.FirstOrDefaultAsync(sc => sc.CategoryKey == categoryKey);
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            return await (
                from c in this.context.Categories.AsNoTracking()
                join sc in this.context.Subcategories.AsNoTracking()
                    on c.CategoryId equals sc.CategoryId
                join e in this.context.DirectoryEntries.AsNoTracking()
                    on sc.SubCategoryId equals e.SubCategoryId
                where e.DirectoryStatus != DirectoryStatus.Removed
                   && e.DirectoryStatus != DirectoryStatus.Unknown
                select c)
            .Distinct()
            .OrderBy(c => c.Name)
            .ToListAsync();
        }

        public async Task<Dictionary<int, DateTime>> GetAllCategoriesLastChangeDatesAsync()
        {
            var categories = await this.context.Categories
                .Select(c => new
                {
                    c.CategoryId,
                    LastModified = c.UpdateDate.HasValue && c.UpdateDate > c.CreateDate ? c.UpdateDate.Value : c.CreateDate
                })
                .ToListAsync();

            return categories.ToDictionary(c => c.CategoryId, c => c.LastModified);
        }
    }
}
