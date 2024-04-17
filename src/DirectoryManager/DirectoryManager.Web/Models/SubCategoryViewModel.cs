namespace DirectoryManager.Web.Models
{
    public class SubCategoryViewModel
    {
        public string CategoryKey { get; set; }
        public string Name { get; set; }
        public string SubCategoryKey { get; set; }
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