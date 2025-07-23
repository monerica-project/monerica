using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    // /Models/NavigationViewModel.cs
    public class NavigationViewModel
    {
        public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
        public Dictionary<int, List<Subcategory>> SubsByCategory { get; set; } = [];
        public string? CurrentCategoryKey { get; set; }
        public string? CurrentSubCategoryKey { get; set; }
    }
}