namespace DirectoryManager.Data.Models.TransferModels
{
    // in DirectoryManager.Data.Models.TransferModels (or wherever you like)
    public class TagCount
    {
        public int TagId { get; set; }
        public string Name { get; set; } = null!;
        public string Key { get; set; } = null!;
        public int Count { get; set; }
    }

}
