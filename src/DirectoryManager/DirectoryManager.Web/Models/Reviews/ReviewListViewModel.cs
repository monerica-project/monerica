using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    /// <summary>One page of the admin reviews list.</summary>
    public sealed class ReviewListViewModel
    {
        public IReadOnlyList<DirectoryEntryReview> Items { get; init; } = Array.Empty<DirectoryEntryReview>();

        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = 50;

        public int Total { get; init; }

        public int TotalPages => this.PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(this.Total / (double)this.PageSize));

        /// <summary>1-based index of the first item on this page (0 when empty).</summary>
        public int FirstItem => this.Total == 0 ? 0 : ((this.Page - 1) * this.PageSize) + 1;

        public int LastItem => Math.Min(this.Total, this.Page * this.PageSize);
    }
}
