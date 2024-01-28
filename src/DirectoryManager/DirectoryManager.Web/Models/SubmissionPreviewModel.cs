namespace DirectoryManager.Web.Models
{
    public class SubmissionPreviewModel
    {
        required public DirectoryEntryViewModel DirectoryEntryViewModel { get; set; }
        public string? NoteToAdmin { get; set; }
        public int SubmissionId { get; set; }
    }
}