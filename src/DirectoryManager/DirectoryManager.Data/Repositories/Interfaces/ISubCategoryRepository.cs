using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubcategoryRepository
    {
        Task<IReadOnlyList<SubcategoryDto>> GetAllAsync();
        Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync();
        Task<IEnumerable<Subcategory>> GetAllActiveSubCategoriesAsync(int minimumInSubCategory);
        Task<Subcategory?> GetByIdAsync(int subCategoryId);
        Task<Subcategory?> GetByCategoryIdAndKeyAsync(int categoryId, string subCategoryKey);
        Task CreateAsync(Subcategory subCategory);
        Task UpdateAsync(Subcategory subCategory);
        Task DeleteAsync(int subCategoryId);
        Task<IEnumerable<Subcategory>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<Subcategory>> GetActiveSubcategoriesAsync(int categoryId);
        Task<Dictionary<int, DateTime>> GetAllSubCategoriesLastChangeDatesAsync();
    }
}