namespace DirectoryManager.Web.Models
{
    public class SiteFileListModel
    {
        public List<SiteFileItem> FileItems { get; set; } = new List<SiteFileItem>();

        public string ParentDirectory { get; set; } = default!;

        public string CurrentDirectory { get; set; } = default!;
    }
}
