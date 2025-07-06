using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    // Data/Models/SearchLog.cs
    public class SearchLog : CreatedStateInfo
    {
        public int Id { get; set; }
        public string Term { get; set; } = null!;
        public string? IpAddress { get; set; }
    }
}
