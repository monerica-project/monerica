using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class CategorySubCategoriesViewModel
    {
        public string PageTitle { get; set; }
        public string PageHeader { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public int SubCategoryId { get; set; }
        public IEnumerable<DirectoryEntry> DirectoryEntries { get; set; }
    }
}
