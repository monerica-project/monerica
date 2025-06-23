namespace DirectoryManager.Web.Models
{
    public class CategoryViewModel
    {
        required public string PageTitle { get; set; }
        required public string PageHeader { get; set; }
        public string? MetaDescription { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        required public IEnumerable<SubCategoryViewModel> SubCategoryItems { get; set; }
        required public int CategoryId { get; set; }
        required public string CategoryName { get; set; }
        required public string CategoryKey { get; set; }
    }
}