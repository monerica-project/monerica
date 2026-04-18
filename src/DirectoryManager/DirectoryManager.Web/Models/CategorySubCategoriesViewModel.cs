using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class CategorySubCategoriesViewModel
    {
        required public string PageTitle { get; set; }
        required public string PageHeader { get; set; }
        public string? Description { get; set; }
        public string? MetaDescription { get; set; }
        public string? PageDetails { get; set; }
        public string? Note { get; set; }
        public int SubCategoryId { get; set; }
        required public string CategoryRelativePath { get; set; }
        required public string CategoryName { get; set; }
        required public string SubcategoryName { get; set; }
        required public string SubCategoryKey { get; set; }
        public Category? Category { get; set; }
        public PagedResult<DirectoryEntry> PagedEntries { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public List<DirectoryEntryViewModel> EntryViewModels { get; set; } = new ();
    }
}