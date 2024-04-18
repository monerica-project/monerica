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
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
    }
}