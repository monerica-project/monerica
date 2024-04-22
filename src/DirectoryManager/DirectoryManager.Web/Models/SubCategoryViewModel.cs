namespace DirectoryManager.Web.Models
{
    public class SubCategoryViewModel
    {
        required public string CategoryKey { get; set; }
        required public string Name { get; set; }
        required public string SubCategoryKey { get; set; }
        public string? Description { get; set; }
        public string SubCategoryRelativePath
        {
            get
            {
                return string.Format("/{0}/{1}", this.CategoryKey, this.SubCategoryKey);
            }
        }
    }
}