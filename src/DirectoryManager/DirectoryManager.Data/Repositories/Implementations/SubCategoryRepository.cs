using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubcategoryRepository : ISubcategoryRepository
    {
        private readonly IApplicationDbContext context;

        public SubcategoryRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IReadOnlyList<SubcategoryDto>> GetAllDtoAsync()
        {
            return await this.context.Subcategories
              .AsNoTracking()
              .OrderBy(s => s.Category.Name)
              .ThenBy(s => s.Name)
              .Select(s => new SubcategoryDto
              {
                  SubcategoryId = s.SubCategoryId,
                  Name = s.Name,
                  Key = s.SubCategoryKey,
                  CategoryId = s.CategoryId,
                  CategoryName = s.Category!.Name,
                  IsEnabled = s.IsEnabled
              })
              .ToListAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync()
        {
            // Active = has a live listing whose main OR additional subcategory is this one.
            var q = this.context.Subcategories
                .Where(sc =>
                    this.ActiveDirectoryEntries().Any(e => e.SubCategoryId == sc.SubCategoryId) ||
                    this.ActiveAdditionalSubcategories().Any(a => a.SubCategoryId == sc.SubCategoryId))
                .Include(sc => sc.Category)
                .AsNoTracking()
                .OrderBy(sc => sc.Name);

            return await q.ToListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync(int minimumInSubcategory)
        {
            return await this.GetFilteredActiveSubCategoriesAsync(minimumInSubcategory);
        }

        public async Task<Subcategory?> GetByIdAsync(int subCategoryId)
        {
            return await this.context.Subcategories
                .Include(sc => sc.Category)
                .FirstOrDefaultAsync(sc => sc.SubCategoryId == subCategoryId);
        }

        public async Task CreateAsync(Subcategory subCategory)
        {
            await this.context.Subcategories.AddAsync(subCategory);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Subcategory subCategory)
        {
            this.context.Subcategories.Update(subCategory);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int subCategoryId)
        {
            var subCategoryToDelete = await this.context.Subcategories.FindAsync(subCategoryId);
            if (subCategoryToDelete != null)
            {
                this.context.Subcategories.Remove(subCategoryToDelete);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<Subcategory?> GetByCategoryIdAndKeyAsync(int categoryId, string subCategoryKey)
        {
            return await this.context.Subcategories
                .Where(subCategory =>
                    subCategory.CategoryId == categoryId &&
                    subCategory.SubCategoryKey == subCategoryKey &&
                    (this.ActiveDirectoryEntries().Any(entry => entry.SubCategoryId == subCategory.SubCategoryId) ||
                     this.ActiveAdditionalSubcategories().Any(a => a.SubCategoryId == subCategory.SubCategoryId)))
                .OrderBy(subCategory => subCategory.Name)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> KeyExistsAsync(int categoryId, string subCategoryKey, int? excludeSubCategoryId = null)
        {
            return await this.context.Subcategories
                .AnyAsync(s => s.CategoryId == categoryId &&
                               s.SubCategoryKey == subCategoryKey &&
                               (excludeSubCategoryId == null || s.SubCategoryId != excludeSubCategoryId.Value));
        }

        public async Task<IEnumerable<Subcategory>> GetByCategoryAsync(int categoryId)
        {
            return await this.context.Subcategories
                .Where(sc => sc.CategoryId == categoryId)
                .OrderBy(sc => sc.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Subcategory>> GetActiveSubcategoriesAsync(int categoryId)
        {
            return await this.context.Subcategories.AsNoTracking()
                .Where(sc => sc.CategoryId == categoryId &&
                    (this.ActiveDirectoryEntries().Any(e => e.SubCategoryId == sc.SubCategoryId) ||
                     this.ActiveAdditionalSubcategories().Any(a => a.SubCategoryId == sc.SubCategoryId)))
                .OrderBy(sc => sc.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<int, DateTime>> GetAllSubCategoriesLastChangeDatesAsync()
        {
            var subCategories = await this.context.Subcategories
                .Select(sc => new
                {
                    sc.SubCategoryId,
                    LastModified = sc.UpdateDate.HasValue && sc.UpdateDate > sc.CreateDate
                        ? sc.UpdateDate.Value
                        : sc.CreateDate
                })
                .ToListAsync();

            return subCategories.ToDictionary(sc => sc.SubCategoryId, sc => sc.LastModified);
        }

        public async Task<IReadOnlyList<Subcategory>> GetAllAsync()
        {
            return await this.context.Subcategories
                         .AsNoTracking()
                         .OrderBy(s => s.Category.Name)
                         .ThenBy(s => s.Name)
                         .ToListAsync();
        }

        private async Task<IEnumerable<Subcategory>> GetFilteredActiveSubCategoriesAsync(int? minimumInSubcategory = null)
        {
            var query = this.context.Subcategories
                .Include(sc => sc.Category)
                .Where(subCategory =>
                    this.ActiveDirectoryEntries().Any(entry => entry.SubCategoryId == subCategory.SubCategoryId) ||
                    this.ActiveAdditionalSubcategories().Any(a => a.SubCategoryId == subCategory.SubCategoryId));

            if (minimumInSubcategory.HasValue)
            {
                query = query.Where(subCategory =>
                    this.ActiveDirectoryEntries().Count(entry => entry.SubCategoryId == subCategory.SubCategoryId)
                    + this.ActiveAdditionalSubcategories().Count(a => a.SubCategoryId == subCategory.SubCategoryId)
                    >= minimumInSubcategory.Value);
            }

            return await query.OrderBy(sc => sc.Name).ToListAsync();
        }

        // Additional-subcategory rows whose listing is live (status is shared with the main listing).
        private IQueryable<DirectoryEntryAdditionalSubcategory> ActiveAdditionalSubcategories() =>
            this.context.DirectoryEntryAdditionalSubcategories
                .Where(a => a.DirectoryEntry.DirectoryStatus != DirectoryStatus.Unknown &&
                            a.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed);

        // Reusable status filter for directory entries
        private IQueryable<DirectoryEntry> ActiveDirectoryEntries() =>
            this.context.DirectoryEntries
                .Where(entry => entry.DirectoryStatus != DirectoryStatus.Unknown &&
                                entry.DirectoryStatus != DirectoryStatus.Removed);
    }
}