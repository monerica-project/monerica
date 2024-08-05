using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubCategoryRepository : ISubCategoryRepository
    {
        private readonly IApplicationDbContext context;

        public SubCategoryRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<Subcategory>> GetAllAsync()
        {
            return await this.context.SubCategories
                            .Include(e => e.Category)
                            .OrderBy(e => e.Category.Name)
                            .OrderBy(e => e.Name)
                            .ToListAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync()
        {
            return await this.context.SubCategories
                        .Include(sc => sc.Category) // Including the Category for each SubCategory
                        .Where(subCategory => this.context.DirectoryEntries
                            .Any(entry => entry.SubCategoryId == subCategory.SubCategoryId &&
                                          entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                          entry.DirectoryStatus != DirectoryStatus.Removed))
                        .OrderBy(subCategory => subCategory.Name)
                        .ToListAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync(int minimumInSubcategory)
        {
            return await this.context.SubCategories
                            .Include(sc => sc.Category) // Including the Category for each SubCategory
                            .Where(subCategory => this.context.DirectoryEntries
                                .Count(entry => entry.SubCategoryId == subCategory.SubCategoryId &&
                                                entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                                entry.DirectoryStatus != DirectoryStatus.Removed) >= minimumInSubcategory)
                            .OrderBy(subCategory => subCategory.Name)
                            .ToListAsync();
        }

        public async Task<Subcategory?> GetByIdAsync(int subCategoryId)
        {
            return await this.context.SubCategories.FindAsync(subCategoryId);
        }

        public async Task CreateAsync(Subcategory subCategory)
        {
            await this.context.SubCategories.AddAsync(subCategory);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Subcategory subCategory)
        {
            this.context.SubCategories.Update(subCategory);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int subCategoryId)
        {
            var subCategoryToDelete = await this.context.SubCategories.FindAsync(subCategoryId);
            if (subCategoryToDelete != null)
            {
                this.context.SubCategories.Remove(subCategoryToDelete);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<Subcategory?> GetByCategoryIdAndKeyAsync(int categoryId, string subCategoryKey)
        {
            return await this.context
                             .SubCategories
                             .Where(subCategory =>
                                subCategory.CategoryId == categoryId &&
                                subCategory.SubCategoryKey == subCategoryKey &&
                                this.context.DirectoryEntries
                                  .Any(entry => entry.SubCategoryId == subCategory.SubCategoryId &&
                                                entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                                entry.DirectoryStatus != DirectoryStatus.Removed))
                             .OrderBy(subCategory => subCategory.Name)
                             .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetByCategoryAsync(int categoryId)
        {
            return await this.context.SubCategories
                                 .Where(sc => sc.CategoryId == categoryId)
                                 .OrderBy(sc => sc.Name)
                                 .ToListAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetActiveSubCategoriesAsync(int categoryId)
        {
            var activeSubCategories = await this.context.SubCategories
                          .Where(subCategory => subCategory.CategoryId == categoryId &&
                              this.context.DirectoryEntries
                                  .Any(entry => entry.SubCategoryId == subCategory.SubCategoryId &&
                                                entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                                entry.DirectoryStatus != DirectoryStatus.Removed))
                          .OrderBy(subCategory => subCategory.Name)
                          .ToListAsync();

            return activeSubCategories;
        }
    }
}