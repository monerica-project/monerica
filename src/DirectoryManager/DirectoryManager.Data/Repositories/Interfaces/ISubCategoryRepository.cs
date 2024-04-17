using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubCategoryRepository
    {
        Task<IEnumerable<SubCategory>> GetAllAsync();
        Task<IEnumerable<SubCategory>> GetAllActiveSubCategoriesAsync();
        Task<SubCategory?> GetByIdAsync(int subCategoryId);
        Task<SubCategory?> GetByCategoryIdAndKeyAsync(int categoryId, string subCategoryKey);
        Task CreateAsync(SubCategory subCategory);
        Task UpdateAsync(SubCategory subCategory);
        Task DeleteAsync(int subCategoryId);
        Task<IEnumerable<SubCategory>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<SubCategory>> GetActiveSubCategoriesAsync(int categoryId);
    }
}