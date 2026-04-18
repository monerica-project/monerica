namespace DirectoryManager.Data.Models.TransferModels
{
    public class TagWithLastModified
    {
        public int TagId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}
