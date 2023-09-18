using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubCategoryRepository
    {
        Task<IEnumerable<SubCategory>> GetAllAsync();
        Task<SubCategory?> GetByIdAsync(int id);
        Task CreateAsync(SubCategory subCategory);
        Task UpdateAsync(SubCategory subCategory);
        Task DeleteAsync(int id);
        Task<SubCategory?> GetByNameAsync(string subCategoryName);
        Task<IEnumerable<SubCategory>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<SubCategory>> GetActiveSubCategoriesAsync(int categoryId);
    }
}