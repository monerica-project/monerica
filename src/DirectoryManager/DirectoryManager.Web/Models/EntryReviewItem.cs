namespace DirectoryManager.Web.Models
{
    public class EntryReviewItem
    {
        public byte? Rating { get; set; }
        public string Body { get; set; } = string.Empty;
        public string AuthorFingerprint { get; set; } = string.Empty;
        public string? AuthorDisplay { get; set; }

        public DateTime CreateDate { get; set; }
    }
}
