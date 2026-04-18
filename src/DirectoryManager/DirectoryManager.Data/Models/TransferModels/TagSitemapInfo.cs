namespace DirectoryManager.Data.Models.TransferModels
{
    public class TagSitemapInfo
    {
        public int TagId { get; set; }
        public string Slug { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int EntryCount { get; set; }
    }
}
