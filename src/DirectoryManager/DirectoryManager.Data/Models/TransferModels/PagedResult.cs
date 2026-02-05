namespace DirectoryManager.Data.Models.TransferModels
{
    public class PagedResult<T>
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    }
}