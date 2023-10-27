namespace DirectoryManager.Web.Models
{
    public class SiteFileItem
    {
        public bool IsFolder { get; set; } = false;

        public string FolderName { get; set; } = default!;

        public string FolderPathFromRoot { get; set; } = default!;

        public string FilePath { get; set; } = default!;

        public string CdnLink { get; set; } = default!;
    }
}
