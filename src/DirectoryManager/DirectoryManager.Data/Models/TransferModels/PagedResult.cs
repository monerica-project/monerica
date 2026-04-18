namespace DirectoryManager.Data.Models.TransferModels
{
    public class PagedResult<T>
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int TotalPages => (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);
        public bool HasPrev => this.Page > 1;
        public bool HasNext => this.Page < this.TotalPages;
    }
}