namespace DirectoryManager.Data.Models
{
    // Models/PagedResult.cs
    public class PagedResult<T>
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    }

}
