using System.Globalization;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Models.TrustPages
{
    /// <summary>The public /reviews page.</summary>
    public sealed class PublicReviewsViewModel
    {
        public PublicReviewFilter Filter { get; init; } = new ();

        public PagedResult<DirectoryEntryReview> Result { get; init; } = new () { PageSize = PublicReviewFilter.PageSize, Page = 1 };

        public IReadOnlyList<Category> Categories { get; init; } = Array.Empty<Category>();

        public IReadOnlyList<ReviewTag> Tags { get; init; } = Array.Empty<ReviewTag>();

        /// <summary>Approved reply count per review id.</summary>
        public IReadOnlyDictionary<int, int> ReplyCounts { get; init; } = new Dictionary<int, int>();

        /// <summary>The listing review page (1-based) each review is on.</summary>
        public IReadOnlyDictionary<int, int> ListingPages { get; init; } = new Dictionary<int, int>();

        public int TotalPages => DirectoryManager.Data.Helpers.PagingHelper.TotalPages(this.Result.TotalCount, this.Result.PageSize);
    }
}
