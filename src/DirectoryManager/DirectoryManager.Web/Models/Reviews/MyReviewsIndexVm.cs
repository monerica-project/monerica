using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    public class MyReviewsIndexVm
    {
        public string Fingerprint { get; set; } = string.Empty;

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalReviews { get; set; }
        public int TotalPages => this.PageSize <= 0 ? 1 : (int)Math.Ceiling(this.TotalReviews / (double)this.PageSize);

        public List<MyReviewRowVm> Reviews { get; set; } = new ();
    }
}
