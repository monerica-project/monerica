namespace DirectoryManager.Web.Models
{
    public class ConfirmDeleteFolderModel
    {
        // Raw value posted back to DeleteFolderAsync (e.g. "/exchange-reviews/").
        public string FolderUrl { get; set; } = default!;

        // Display name of the folder being deleted (last path segment).
        public string FolderName { get; set; } = default!;

        // Real files (folder prefix stripped) that will be permanently deleted.
        public List<string> Files { get; set; } = new List<string>();

        // Count of internal "_.txt" folder-marker blobs (not shown individually).
        public int MarkerFileCount { get; set; }
    }
}
