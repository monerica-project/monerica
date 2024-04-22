using DirectoryManager.Data.Models;

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
        required public IEnumerable<DirectoryEntry> DirectoryEntries { get; set; }

    }
}
