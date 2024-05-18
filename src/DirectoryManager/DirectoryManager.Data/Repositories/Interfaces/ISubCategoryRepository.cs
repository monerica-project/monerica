using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubCategoryRepository
    {
        Task<IEnumerable<Subcategory>> GetAllAsync();
        Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync();
        Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync(int minimumInSubCategory);

        Task<Subcategory?> GetByIdAsync(int subCategoryId);
        Task<Subcategory?> GetByCategoryIdAndKeyAsync(int categoryId, string subCategoryKey);
        Task CreateAsync(Subcategory subCategory);
        Task UpdateAsync(Subcategory subCategory);
        Task DeleteAsync(int subCategoryId);
        Task<IEnumerable<Subcategory>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<Subcategory>> GetActiveSubCategoriesAsync(int categoryId);
    }
}