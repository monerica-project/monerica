namespace DirectoryManager.Web.Models
{
    /// <summary>One category's sponsored/featured spots on /sponsors-and-featured.</summary>
    public class SpotCategoryGroup
    {
        public string CategoryName { get; set; } = string.Empty;

        public string CategoryPath { get; set; } = string.Empty;

        public List<SpotRow> Rows { get; set; } = new ();
    }
}
