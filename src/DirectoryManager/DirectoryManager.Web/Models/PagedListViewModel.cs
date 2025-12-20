namespace DirectoryManager.Web.Models
{
    public class PagedListViewModel<T>
    {
        public required IReadOnlyList<T> Items { get; init; }

        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }

        public int TotalPages => this.PageSize <= 0
            ? 0
            : (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);

        public bool HasPrevious => this.Page > 1;
        public bool HasNext => this.Page < this.TotalPages;

        public int FirstItemNumber => this.TotalCount == 0 ? 0 : ((this.Page - 1) * this.PageSize) + 1;
        public int LastItemNumber => this.TotalCount == 0 ? 0 : Math.Min(this.Page * this.PageSize, this.TotalCount);
    }
}
