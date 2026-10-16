using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(int categoryId);
        Task CreateAsync(Category category);
        Task UpdateAsync(Category category);
        Task DeleteAsync(int categoryId);
        Task<Category> GetByNameAsync(string categoryName);
        Task<Category?> GetByKeyAsync(string categoryKey);

        /// <summary>True when another category (not <paramref name="excludeCategoryId"/>) already uses this key.</summary>
        Task<bool> KeyExistsAsync(string categoryKey, int? excludeCategoryId = null);
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
        Task<Dictionary<int, DateTime>> GetAllCategoriesLastChangeDatesAsync();
    }
}