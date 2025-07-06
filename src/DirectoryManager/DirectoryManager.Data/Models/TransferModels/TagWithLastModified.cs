
namespace DirectoryManager.Data.Models.TransferModels
{
    public class TagWithLastModified
    {
        public int TagId { get; set; }
        public string Name { get; set; } = "";
        public DateTime LastModified { get; set; }
    }
}
